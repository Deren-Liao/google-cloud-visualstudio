﻿// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using EnvDTE;
using EnvDTE80;
using GoogleCloudExtension.Utils;
using Shell = Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace GoogleCloudExtension.AttachDebuggerDialog
{
    /// <summary>
    /// This step get a list of processes from remote machine.
    /// User chooses the process and attach to it.
    /// </summary>
    public class ListProcessStepViewModel : AttachDebuggerStepBase
    {
        private readonly static string s_detectEngineTypeItemName = Resources.AttachDebuggerAutomaticallyDetectEngineTypeItemName;

        private string _message;
        private ProcessItem _selectedProcess;
        private IEnumerable<ProcessItem> _processesToChoose;
        private List<ProcessItem> _allProcesses;
        private bool _showList;

        private List<string> _engineTypes;
        private string _selectedEngine;
        private bool _saveSelection = true;

        /// <summary>
        /// The progress message to display.
        /// </summary>
        public string ProgressMessage
        {
            get { return _message; }
            private set { SetValueAndRaise(out _message, value); }
        }

        /// <summary>
        /// Controls whether to show processes list.
        /// </summary>
        public bool IsListVisible
        {
            get { return _showList; }
            private set { SetValueAndRaise(out _showList, value); }
        }

        /// <summary>
        /// The list of processes shown to user to pick one.
        /// </summary>
        public IEnumerable<ProcessItem> Processes
        {
            get { return _processesToChoose; }
            private set { SetValueAndRaise(out _processesToChoose, value); }
        }

        /// <summary>
        /// Gets or sets the selected process.
        /// </summary>
        public ProcessItem SelectedProcess
        {
            get { return _selectedProcess; }
            set { SetValueAndRaise(out _selectedProcess, value); }
        }

        /// <summary>
        /// The debugger engine type lists that user can pick one.
        /// </summary>
        public List<string> EngineTypes
        {
            get { return _engineTypes; }
            private set { SetValueAndRaise(out _engineTypes, value); }
        }

        /// <summary>
        /// Gets or sets the selected debugger engine.
        /// </summary>
        public string SelectedEngine
        {
            get { return _selectedEngine; }
            set { SetValueAndRaise(out _selectedEngine, value); }
        }

        /// <summary>
        /// Indicates if user wants to save current selection as preference
        /// so that user does not need to come back to see the dialog again.
        /// </summary>
        public bool SaveSelection
        {
            get { return _saveSelection; }
            set { SetValueAndRaise(out _saveSelection, value); }
        }

        /// <summary>
        /// Responds to Refresh button command.
        /// </summary>
        public ProtectedCommand RefreshCommand { get; }

        #region Implement interface IAttachDebuggerStep

        public override ContentControl Content { get; }

        public override Task<IAttachDebuggerStep> OnStartAsync()
        {
            ProgressMessage = Resources.AttachDebuggerConnectingProgressMessage;
            IsCancelButtonEnabled = false;

            if (!WindowsCredentialManager.Write(
                Context.PublicIp,
                Context.Credential.User,
                Context.Credential.Password))
            {
                Debug.WriteLine($"Failed to save credential for {Context.PublicIp}, last error is {Marshal.GetLastWin32Error()}");
                // It's OKay to continue, the Debugger2 will prompt UI to ask for credential. 
            }
            if (!GetAllProcessesList())
            {
                Context.DialogWindow.Close();
                return null;    // TODO: add a help page step.
            }
            else if (Processes.Count() == 1)
            {
                return Task.FromResult(Attach());
            }
            else
            {
                EnableSelection();
                return Task.FromResult<IAttachDebuggerStep>(null);
            }
        }

        public override Task<IAttachDebuggerStep> OnOkCommandAsync()
        {
            if (SelectedProcess == null || SelectedEngine == null)
            {
                Debug.WriteLine($"ListProcessStep, OnOkCommandAsync, unexpected error. SelectedProcess or SelectedEngine is null.");
                // The code won't be reached. Just to be safe, return null.
                return Task.FromResult<IAttachDebuggerStep>(null);
            }

            if (SaveSelection)
            {
                AttachDebuggerSettings.Current.DefaultDebuggeeProcessName = SelectedProcess.Name;
                AttachDebuggerSettings.Current.DefaultDebuggerEngineType = SelectedEngine;
            }
            return Task.FromResult(Attach());
        }

        #endregion

        /// <summary>
        /// Create the step that get remote machine processes list and attach to one of the processes.
        /// </summary>
        public static ListProcessStepViewModel CreateStep(AttachDebuggerContext context)
        {
            var content = new ListProcessStepContent();
            var step = new ListProcessStepViewModel(content, context);
            content.DataContext = step;
            return step;
        }

        private IAttachDebuggerStep Attach()
        {
            IsListVisible = false;
            ProgressMessage = String.Format(
                Resources.AttachDebuggerAttachingProcessMessageFormat,
                SelectedProcess.Name);
            try
            {
                if (SelectedEngine == s_detectEngineTypeItemName)
                {
                    SelectedProcess.Process.Attach();
                }
                else
                {
                    SelectedProcess.Process.Attach2(SelectedEngine);
                }
            }
            catch (COMException)
            {
                UserPromptUtils.ErrorPrompt(
                    message: String.Format(Resources.AttachDebuggerAttachErrorMessageFormat, SelectedProcess.Name),
                    title: Resources.uiDefaultPromptTitle);
                ResetDefaultSelection();
                return null;    // TODO: goto help page
            }
            Context.DialogWindow.Close();
            return null;
        }

        /// <summary>
        /// Helper method that set the dialog content to show picking a process controls.
        /// </summary>
        private void EnableSelection()
        {
            IsCancelButtonEnabled = true;
            IsOKButtonEnabled = true;
            ProgressMessage = Resources.AttachDebuggerPickingProcessMessage;
            IsListVisible = true;
        }

        private bool GetAllProcessesList()
        {
            var dte = Shell.Package.GetGlobalService(typeof(DTE)) as DTE;
            var debugger = dte.Debugger as Debugger2;
            var transport = debugger.Transports.Item("Default");

            // Show engine types
            EngineTypes = new List<string>();
            EngineTypes.Add(Resources.AttachDebuggerAutomaticallyDetectEngineTypeItemName);
            foreach (var engineType in transport.Engines)
            {
                var engine = engineType as Engine;
                if (engine == null)
                {
                    Debug.WriteLine("engine is null, might be a code bug.");
                    continue;
                }
                EngineTypes.Add(engine.Name);
            }
            SelectedEngine = s_detectEngineTypeItemName;

            Processes processes = debugger.GetProcesses(transport, Context.PublicIp);
            _allProcesses = new List<ProcessItem>();
            foreach (var process in processes)      // Linq does not work on COM list
            {
                var pro2 = process as Process2;
                _allProcesses.Add(new ProcessItem(pro2));
            }

            if (_allProcesses.Count == 0)
            {
                UserPromptUtils.ErrorPrompt(
                    message: Resources.AttachDebuggerListProcessEmptyResultErrorMessage,
                    title: Resources.uiDefaultPromptTitle);
                return false;
            }

            if (!String.IsNullOrWhiteSpace(AttachDebuggerSettings.Current.DefaultDebuggeeProcessName))
            {
                var matching = _allProcesses
                    .Where(x => x.Name.ToLowerInvariant() == AttachDebuggerSettings.Current.DefaultDebuggeeProcessName.ToLowerInvariant());
                if (!matching.Any())
                {
                    ResetDefaultSelection();
                }
                else if (matching.Count() == 1)
                {
                    Processes = matching;
                    SelectedProcess = matching.First();
                    SelectedEngine = AttachDebuggerSettings.Current.DefaultDebuggerEngineType;
                    return true;
                }
            }

            Processes = _allProcesses;
            SelectedProcess = Processes.FirstOrDefault();
            return true;
        }

        private ListProcessStepViewModel(
            ListProcessStepContent content,
            AttachDebuggerContext context)
            : base(context)
        {
            Content = content;
            RefreshCommand = new ProtectedCommand(() => GetAllProcessesList());
        }

        private void ResetDefaultSelection()
        {
            AttachDebuggerSettings.Current.DefaultDebuggeeProcessName = "";
            AttachDebuggerSettings.Current.DefaultDebuggerEngineType = "";
        }
    }
}
