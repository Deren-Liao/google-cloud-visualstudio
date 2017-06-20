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

using Google.Apis.CloudResourceManager.v1.Data;
using Google.Apis.CloudSourceRepositories.v1.Data;
using GoogleCloudExtension.Accounts;
using GoogleCloudExtension.DataSources;
using GoogleCloudExtension.GitUtils;
using GoogleCloudExtension.Theming;
using GoogleCloudExtension.Utils;
using GoogleCloudExtension.Utils.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// View model for user control CsrCloneWindowContent.xaml.
    /// </summary>
    public abstract class CsrCloneCreateViewModelBase : ValidatingViewModelBase
    {
        protected CommonDialogWindowBase _owner;

        private string _localPath;
        private IEnumerable<Project> _projects;
        private Project _selectedProject;
        private bool _isReady = true;

        /// <summary>
        /// The projects list
        /// </summary>
        public IEnumerable<Project> Projects
        {
            get { return _projects; }
            private set { SetValueAndRaise(ref _projects, value); }
        }

        /// <summary>
        /// Selected project
        /// </summary>
        public Project SelectedProject
        {
            get { return _selectedProject; }
            set
            {
                var oldValue = _selectedProject;
                SetValueAndRaise(ref _selectedProject, value);
                if (_selectedProject != null && _isReady && oldValue != _selectedProject)
                {
                    ErrorHandlerUtils.HandleAsyncExceptions(() => ExecuteAsync(ListRepoAsync));
                }
            }
        }

        /// <summary>
        /// The local path to clone the repository to 
        /// </summary>
        public string LocalPath
        {
            get { return _localPath; }
            set
            {
                SetValueAndRaise(ref _localPath, value);
                ValidateInputs();
            }
        }

        /// <summary>
        /// Indicates if there is async task running that UI should be disabled.
        /// </summary>
        public bool IsReady
        {
            get { return _isReady; }
            set { SetValueAndRaise(ref _isReady, value); }
        }

        /// <summary>
        /// Responds to choose a folder command
        /// </summary>
        public ICommand PickFolderCommand { get; }

        /// <summary>
        /// Responds to OK button click event
        /// </summary>
        virtual public ProtectedCommand OkCommand { get; }

        virtual protected string RepoName { get; }

        /// <summary>
        /// Final cloned repository
        /// </summary>
        public RepoItemViewModel Result { get; protected set; }

        public CsrCloneCreateViewModelBase(CommonDialogWindowBase owner)
        {
            _owner = owner.ThrowIfNull(nameof(owner));
            PickFolderCommand = new ProtectedCommand(PickFoloder);
            ErrorHandlerUtils.HandleAsyncExceptions(() => ExecuteAsync(InitializeAsync));
        }

        //private async Task CloneAsync()
        //{
        //    // If OkCommand is enabled, SelectedRepository and LocalPath is valid
        //    string destPath = Path.Combine(LocalPath, CsrUtils.GetRepoName(SelectedRepository));

        //    if (!CsrGitUtils.StoreCredential(
        //        SelectedRepository.Url,
        //        CredentialsStore.Default.CurrentAccount.RefreshToken,
        //        useHttpPath: true))
        //    {
        //        UserPromptUtils.ErrorPrompt(
        //            message: Resources.CsrCloneFailedMessage,
        //            title: Resources.uiDefaultPromptTitle);
        //        return;
        //    }

        //    GitRepository localRepo = await CsrGitUtils.Clone(SelectedRepository.Url, destPath);
        //    if (localRepo == null)
        //    {
        //        UserPromptUtils.ErrorPrompt(
        //            message: Resources.CsrCloneFailedToSetCredentialMessage,
        //            title: Resources.uiDefaultPromptTitle);
        //        return;
        //    }
        //    Result = new RepoItemViewModel(SelectedRepository, localRepo.Root);
        //    _owner.Close();
        //}

        private void PickFoloder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = Resources.CsrCloneLocalPathLabel;
                dialog.SelectedPath = LocalPath;
                dialog.ShowNewFolderButton = true;
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LocalPath = dialog.SelectedPath;
                }
            }
        }

        protected async Task ExecuteAsync(Func<Task> task)
        {
            IsReady = false;
            try
            {
                await task();
            }
            finally
            {
                IsReady = true;
            }
        }

        protected abstract Task ListRepoAsync();

        protected async Task InitializeAsync()
        {
            Debug.WriteLine("Init");

            ResourceManagerDataSource resourceManager = DataSourceFactories.CreateResourceManagerDataSource();
            if (resourceManager == null)
            {
                return;
            }

            Projects = await resourceManager.GetSortedActiveProjectsAsync();
            if (Projects.Any())
            {
                SelectedProject = Projects.FirstOrDefault();
                await ListRepoAsync();
            }
            else
            {
                UserPromptUtils.ErrorPrompt(
                    message: Resources.CsrCloneNoProject,
                    title: Resources.uiDefaultPromptTitle);
                _owner.Close();
            }
        }

        protected virtual void ValidateRepoName() { }

        protected void ValidateInputs()
        {
            ValidateRepoName();
            SetValidationResults(
                ValidateLocalPath(LocalPath, Resources.CsrCloneLocalPathFieldName), 
                nameof(LocalPath));
            OkCommand.CanExecuteCommand = RepoName != null && !HasErrors;
        }

        protected IEnumerable<ValidationResult> ValidateLocalPath(string path, string fieldName)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                yield return StringValidationResult.FromResource(
                    nameof(Resources.ValdiationNotEmptyMessage), fieldName);
                yield break;
            }
            if (!Directory.Exists(path))
            {
                yield return StringValidationResult.FromResource(nameof(Resources.CsrClonePathNotExistMessage));
                yield break;
            }
            if (RepoName != null)
            {
                string destPath = Path.Combine(LocalPath, RepoName);
                if (Directory.Exists(destPath) && !PathUtils.IsPathEmpth(destPath))
                {
                    yield return StringValidationResult.FromResource(
                        nameof(Resources.CsrClonePathExistNotEmptyMessageFormat), destPath);
                    yield break;
                }
            }
        }

        protected async Task CloneAsync(Repo CloudRepo)
        {
            if (CloudRepo == null)
            {
                return;
            }

            // If OkCommand is enabled, SelectedRepository and LocalPath is valid
            string destPath = Path.Combine(LocalPath, RepoName);

            if (!CsrGitUtils.StoreCredential(
                CloudRepo.Url,
                CredentialsStore.Default.CurrentAccount.RefreshToken,
                useHttpPath: true))
            {
                UserPromptUtils.ErrorPrompt(
                    message: Resources.CsrCloneFailedMessage,
                    title: Resources.uiDefaultPromptTitle);
                return;
            }

            GitRepository localRepo = await CsrGitUtils.Clone(CloudRepo.Url, destPath);
            if (localRepo == null)
            {
                UserPromptUtils.ErrorPrompt(
                    message: Resources.CsrCloneFailedToSetCredentialMessage,
                    title: Resources.uiDefaultPromptTitle);
                return;
            }
            Result = new RepoItemViewModel(CloudRepo, localRepo.Root);
            _owner.Close();
        }
    }
}
