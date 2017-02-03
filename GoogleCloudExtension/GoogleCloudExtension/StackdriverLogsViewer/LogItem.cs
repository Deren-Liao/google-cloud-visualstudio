﻿// Copyright 2016 Google Inc. All Rights Reserved.
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

using Google.Apis.Logging.v2.Data;
using GoogleCloudExtension.StackdriverLogsViewer.TreeViewConverters;
using GoogleCloudExtension.SolutionUtils;
using GoogleCloudExtension.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    /// <summary>
    /// An adaptor to LogEntry so as to provide properties for data binding.
    /// </summary>
    internal class LogItem : ViewModelBase
    {
        private const string SourceFileFieldName = "source_file";
        private const string SourceLineFieldName = "source_line";
        private const string AssemblyNameFieldName = "assembly_name";
        private const string AssemblyVersionFieldName = "assembly_version";
        private const string JasonPayloadMessageFieldName = "message";
        private const string AnyIconPath = "StackdriverLogsViewer/Resources/ic_log_level_any_12.png";
        private const string DebugIconPath = "StackdriverLogsViewer/Resources/ic_log_level_debug_12.png";
        private const string ErrorIconPath = "StackdriverLogsViewer/Resources/ic_log_level_error_12.png";
        private const string FatalIconPath = "StackdriverLogsViewer/Resources/ic_log_level_fatal_12.png";
        private const string InfoIconPath = "StackdriverLogsViewer/Resources/ic_log_level_info_12.png";
        private const string WarningIconPath = "StackdriverLogsViewer/Resources/ic_log_level_warning_12.png";

        private static readonly Lazy<ImageSource> s_anyIcon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(AnyIconPath));
        private static readonly Lazy<ImageSource> s_debugIcon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(DebugIconPath));
        private static readonly Lazy<ImageSource> s_errorIcon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(ErrorIconPath));
        private static readonly Lazy<ImageSource> s_fatalIcon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(FatalIconPath));
        private static readonly Lazy<ImageSource> s_infoIcon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(InfoIconPath));
        private static readonly Lazy<ImageSource> s_warningIcon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(WarningIconPath));

        private readonly Lazy<List<ObjectNodeTree>> _treeViewObjects;

        private readonly string _sourceFilePath;
        private readonly string _assemblyName;
        private readonly string _assemblyVersion;
        public readonly int SourceLine;

        public IWpfTextView SourceLineTextView { get; private set; }    

        private bool _filterLogsOfSourceLine = true;
        public bool FilterLogsOfSourceLine
        {
            get { return _filterLogsOfSourceLine; }
            set { SetValueAndRaise(ref _filterLogsOfSourceLine, value); }
        }

        /// <summary>
        /// Gets the time stamp.
        /// </summary>
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// Gets a log entry object.
        /// </summary>
        public LogEntry Entry { get; }

        /// <summary>
        /// Gets the log item timestamp Date string in local time. Data binding to a view property.
        /// </summary>
        public string Date => TimeStamp.ToString(Resources.LogViewerLogItemDateFormat);

        /// <summary>
        /// Gets a log item timestamp in local time. Data binding to a view property.
        /// </summary>
        public string Time => TimeStamp.ToString(Resources.LogViewerLogItemTimeFormat);

        /// <summary>
        /// Gets the log message to be displayed at top level.
        /// </summary>
        public string Message => ComposeMessage();

        /// <summary>
        /// Gets the log severity tooltip. Data binding to the severity icon tool tip.
        /// </summary>
        public string SeverityTip => String.IsNullOrWhiteSpace(Entry?.Severity) ?
            Resources.LogViewerAnyOtherSeverityLevelTip : Entry.Severity;

        /// <summary>
        /// Gets the list of ObjectNodeTree for detail tree view.
        /// </summary>
        public List<ObjectNodeTree> TreeViewObjects => _treeViewObjects.Value;

        /// <summary>
        /// Gets the formated source location as content of data grid column.
        /// </summary>
        public string SourceLocation { get; }

        /// <summary>
        /// Command responses to source link button click event.
        /// </summary>
        public ProtectedCommand SourceLinkCommand { get; }

        /// <summary>
        /// Command responses to the back to logs viewer button.
        /// </summary>
        public ProtectedCommand BackToLogsViewerCommand { get; }

        /// <summary>
        /// Gets the log item severity level. The data binding source to severity column in the data grid.
        /// </summary>
        public ImageSource SeverityLevel
        {
            get
            {
                LogSeverity logLevel;
                if (String.IsNullOrWhiteSpace(Entry?.Severity) ||
                    !Enum.TryParse<LogSeverity>(Entry?.Severity, ignoreCase: true, result: out logLevel))
                {
                    return s_anyIcon.Value;
                }

                switch (logLevel)
                {
                    // EMERGENCY, CRITICAL, Alert all map to fatal icon.
                    case LogSeverity.Alert:
                    case LogSeverity.Critical:
                    case LogSeverity.Emergency:
                        return s_fatalIcon.Value;

                    case LogSeverity.Debug:
                        return s_debugIcon.Value;

                    case LogSeverity.Error:
                        return s_errorIcon.Value;

                    case LogSeverity.Notice:
                    case LogSeverity.Info:
                        return s_infoIcon.Value;

                    case LogSeverity.Warning:
                        return s_warningIcon.Value;

                    default:
                        return s_anyIcon.Value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <seealso cref="LogItem"/> class.
        /// </summary>
        /// <param name="logEntry">A log entry.</param>
        public LogItem(LogEntry logEntry)
        {
            Entry = logEntry;
            TimeStamp = ConvertTimestamp(logEntry.Timestamp);
            _treeViewObjects = new Lazy<List<ObjectNodeTree>>(CreateTreeObject);
            BackToLogsViewerCommand = new ProtectedCommand(BackToLogsViewer);
            SourceLinkCommand = new ProtectedCommand(OnSourceLinkClick);
            SourceLine = -1;
            if (null != Entry?.Labels)
            {
                _assemblyName = GetLabelField(AssemblyNameFieldName);
                _sourceFilePath = GetLabelField(SourceFileFieldName);
                _assemblyVersion = GetLabelField(AssemblyVersionFieldName);
                var line = GetLabelField(SourceLineFieldName);
                Int32.TryParse(line, out SourceLine);
                if (!String.IsNullOrWhiteSpace(_sourceFilePath) && SourceLine != -1)
                {
                    SourceLocation = $"{Path.GetFileName(_sourceFilePath)} ({SourceLine})";
                }
            }

            CloseButtonCommand = new ProtectedCommand(OnCloseTooltip);
        }

        /// <summary>
        /// Change time zone of log item.
        /// </summary>
        /// <param name="newTimeZone">The new time zone.</param>
        public void ChangeTimeZone(TimeZoneInfo newTimeZone)
        {
            TimeStamp = TimeZoneInfo.ConvertTime(TimeStamp, newTimeZone);
            RaisePropertyChanged(nameof(Time));
        }

        private List<ObjectNodeTree> CreateTreeObject()
        {
            return new ObjectNodeTree(Entry).Children;
        }

        private string GetLabelField(string fieldName)
        {
            return Entry.Labels.ContainsKey(fieldName) ? Entry.Labels[fieldName] : null;
        }

        private string ComposeDictionaryPayloadMessage(IDictionary<string, object> dictPayload)
        {
            if (dictPayload == null)
            {
                return "";
            }

            StringBuilder text = new StringBuilder();
            foreach (var kv in dictPayload)
            {
                text.AppendFormat(Resources.LogViewerDictionaryPayloadFormatString, kv.Key, kv.Value);
            }

            return text.ToString();
        }

        private string ComposeMessage()
        {
            string message = null;
            if (Entry?.JsonPayload != null)
            {
                // If the JsonPload has message filed, display this field.
                if (Entry.JsonPayload.ContainsKey(JasonPayloadMessageFieldName))
                {
                    message = Entry.JsonPayload[JasonPayloadMessageFieldName].ToString();
                }
                else
                {
                    message = ComposeDictionaryPayloadMessage(Entry.JsonPayload);
                }
            }
            else if (Entry?.ProtoPayload != null)
            {
                message = ComposeDictionaryPayloadMessage(Entry.ProtoPayload);
            }
            else if (Entry?.TextPayload != null)
            {
                message = Entry.TextPayload;
            }
            else if (Entry?.Labels != null)
            {
                message = String.Join(";", Entry?.Labels.Values);
            }
            else if (Entry?.Resource?.Labels != null)
            {
                message = String.Join(";", Entry?.Resource.Labels);
            }

            return message?.Replace("\r\n", "\\r\\n ").Replace("\t", "\\t ").Replace("\n", "\\n ");
        }

        private DateTime ConvertTimestamp(object timestamp)
        {
            DateTime datetime;
            if (timestamp == null)
            {
                Debug.Assert(false, "Entry Timestamp is null");
                datetime = DateTime.MaxValue;
            }
            else if (timestamp is DateTime)
            {
                datetime = (DateTime)timestamp;
            }
            else
            {
                // From Stackdriver Logging API reference,
                // A timestamp in RFC3339 UTC "Zulu" format, accurate to nanoseconds. 
                // Example: "2014-10-02T15:01:23.045123456Z".
                if (!DateTime.TryParse(timestamp.ToString(),
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out datetime))
                {
                    datetime = DateTime.MaxValue;
                }
            }

            return datetime.ToLocalTime();
        }

        #region source line matching

        public ProtectedCommand CloseButtonCommand { get; }

        private void OnCloseTooltip()
        {
            HighlightLogger.HideTooltip(SourceLineTextView);
        }

        private void OnSourceLinkClick()
        {
            Debug.Assert(SourceLine != -1 && _sourceFilePath != null, 
                "There is a code bug if source file or source line is invalid");
            // var sourceFiles = SolutionHelper.CurrentSolution?.FindMatchingSourceFile(_sourceFilePath);

            var project = FindProject();
            if (project == null)
            {
                Debug.WriteLine($"Failed to find project of {_assemblyName}");
                return;
            }

            var sourceFiles = project.SourceFiles.Where(x => x.IsMatchingPath(_sourceFilePath));
            if (!sourceFiles.Any())
            {
                PromptNotfound();
                return;
            }

            var window = sourceFiles.First().GotoLine(SourceLine);
            if (null == window)
            {
                PromptNotfound();
                return;
            }

            SourceLineTextView = HighlightLogger.ShowTip(window, this);
        }

        private const string PromptTitle = "Locating logging source";

        private void PromptNotfound()
        {
            string errorPrompt = @"The log entry does not contain valid source information or the source line can not be located";
            UserPromptUtils.ErrorPrompt(errorPrompt, PromptTitle);
        }

        private void OpenValidProjectPrompt()
        {
            UserPromptUtils.ErrorPrompt(
                $@"The log entry was generated from assembly {_assemblyName}, version {_assemblyVersion}
Please open the project in order to navigate to the logging method source location.", 
                PromptTitle);
        }

        private ProjectHelper FindProject()
        {
            if (String.IsNullOrWhiteSpace(_assemblyName) || String.IsNullOrWhiteSpace(_assemblyVersion))
            {
                UserPromptUtils.ErrorPrompt(
                    @"The log entry does not contain valid assembly name or assembly version", 
                    PromptTitle);
            }

            if (SolutionHelper.CurrentSolution == null)
            {
                OpenValidProjectPrompt();
                return null;
            }

            var project = SolutionHelper.CurrentSolution.Projects?.FirstOrDefault(x => x.AssemblyName == _assemblyName);
            if (project == null)
            {
                OpenValidProjectPrompt();
                return null;
            }

            if (project.Version != _assemblyVersion)
            {
                if (!UserPromptUtils.ActionPrompt(
                        prompt: 
$@"Version missmatch.
The current project {project.Name} version is {project.Version},
Pleae open the project of version {_assemblyVersion},
in order to properly locating the logging source location.",
                    title: PromptTitle,
                    message: "Do you want to continue anyway?" ))
                {
                    return null;
                }
            }

            return project;
        }

        #endregion

        private void BackToLogsViewer()
        {
            var window = ToolWindowUtils.ShowToolWindow<LogsViewerToolWindow>();
            if (Entry == null)
            {
                Debug.WriteLine("Entry is null, this is likely a code bug");
                return;
            }

            if (window == null)
            {
                return;
            }

            if (FilterLogsOfSourceLine)
            {
                StringBuilder filter = new StringBuilder();
                filter.AppendLine($"resource.type=\"{Entry.Resource.Type}\"");
                filter.AppendLine($"logName=\"{Entry.LogName}\"");
                filter.AppendLine($"labels.{SourceFileFieldName}=\"{_sourceFilePath.Replace(@"\", @"\\")}\"");
                filter.AppendLine($"labels.{AssemblyNameFieldName}=\"{_assemblyName}\"");
                filter.AppendLine($"labels.{AssemblyVersionFieldName}=\"{_assemblyVersion}\"");
                filter.AppendLine($"labels.{SourceLineFieldName}=\"{SourceLine}\"");
                window.AdvancedFilter(filter.ToString());
            }
        }
    }
}
