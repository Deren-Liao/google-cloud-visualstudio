// Copyright 2017 Google Inc. All Rights Reserved.
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
using GoogleCloudExtension.GitUtils;
using GoogleCloudExtension.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// View model for user control CsrCreateWindowContent.xaml.
    /// </summary>
    public class CsrCreateWindowViewModel : ViewModelBase
    {
        private CsrCreateWindow _owner;

        private string _localPath;
        private IEnumerable<Project> _projects;
        private Project _selectedProject;
        private bool _isReady = true;
        private string _repoName;
        private bool _gotoCsrWebPage = true;

        public bool GotoCsrWebPage
        {
            get { return _gotoCsrWebPage; }
            set { SetValueAndRaise(ref _gotoCsrWebPage, value); }
        }

        public string RepositoryName
        {
            get { return _repoName; }
            set { SetValueAndRaise(ref _repoName, value); }
        }

        public IEnumerable<Project> Projects
        {
            get { return _projects; }
            private set { SetValueAndRaise(ref _projects, value); }
        }

        public Project SelectedProject
        {
            get { return _selectedProject; }
            set { SetValueAndRaise(ref _selectedProject, value); }
        }

        public string LocalPath
        {
            get { return _localPath; }
            set { SetValueAndRaise(ref _localPath, value); }
        }

        public bool IsReady
        {
            get { return _isReady; }
            set { SetValueAndRaise(ref _isReady, value); }
        }

        public ProtectedCommand PickFolderCommand { get; }

        public ProtectedCommand OkCommand { get; }

        /// <summary>
        /// Final cloned repository
        /// </summary>
        public RepoItemViewModel Result { get; private set; }

        public CsrCreateWindowViewModel(CsrCreateWindow owner)
        {
            _owner = owner.ThrowIfNull(nameof(owner));
            PickFolderCommand = new ProtectedCommand(PickFoloder);
            OkCommand = new ProtectedCommand( 
                () => ErrorHandlerUtils.HandleAsyncExceptions(
                    () =>ExecuteAsync(Create))
                );
            ErrorHandlerUtils.HandleAsyncExceptions(
                    () => ExecuteAsync(Init));
        }

        private async Task Create()
        {
            await ValidateInput();

            var csrDatasource = CsrUtils.CreateCsrDataSource(SelectedProject.ProjectId);
            var repo = await csrDatasource.CreateRepoAsync(RepositoryName);

            GitRepository gitCommand = await CsrGitUtils.Clone(repo.Url, LocalPath);

            Result = new RepoItemViewModel(repo, gitCommand);
            _owner.Close();
        }

        private async Task ValidateInput()
        {
            // TODO: validate data
            if (!(await ValidateRepoName(RepositoryName)))
            {
                UserPromptUtils.ErrorPrompt("Invalid repo name", Resources.uiDefaultPromptTitle);
                return;
            }

            if (String.IsNullOrWhiteSpace(RepositoryName) || String.IsNullOrWhiteSpace(LocalPath)
                || Directory.Exists(LocalPath))
            {
                UserPromptUtils.ErrorPrompt(message: "Invalid input", title: Resources.uiDefaultPromptTitle);
                return;
            }
        }

        private void PickFoloder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Clone to ";
                dialog.SelectedPath = LocalPath;
                dialog.ShowNewFolderButton = true;
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LocalPath = dialog.SelectedPath;
                }
            }
        }

        private async Task ExecuteAsync(Func<Task> task)
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

        private async Task<bool> ValidateRepoName(string name)
        {
            // TODO: Validate name using Regex.

            // TODO: use lazy that caches repos list
            var repos = await CsrUtils.GetCloudReposAsync(SelectedProject);
            return !repos.Any(x => string.Compare(name, x.Name, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private async Task Init()
        {
            Debug.WriteLine("Init");
            Projects = await CsrUtils.GetProjectsAsync();
            if (Projects?.Any() ?? false)
            {
                SelectedProject = Projects.FirstOrDefault();
            }
            else
            {
                UserPromptUtils.ErrorPrompt(
                    message: "No any project is found",
                    title: "Google Cloud Source Repositories");
                _owner.Close();
            }
        }
    }
}
