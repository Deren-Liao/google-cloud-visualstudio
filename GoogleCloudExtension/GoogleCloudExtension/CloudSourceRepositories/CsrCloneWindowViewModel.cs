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
    public class CsrCloneWindowViewModel : CsrCloneCreateViewModelBase
    {
        private IList<Repo> _repos;
        private Repo _selectedRepo;

        /// <summary>
        /// The list of repositories that belong to the project
        /// </summary>
        public IList<Repo> Repositories
        {
            get { return _repos; }
            private set { SetValueAndRaise(ref _repos, value); }
        }

        /// <summary>
        /// Currently selected repository
        /// </summary>
        public Repo SelectedRepository
        {
            get { return _selectedRepo; }
            set
            {
                SetValueAndRaise(ref _selectedRepo, value);
                ValidateInputs();
            }
        }

        public override ProtectedCommand OkCommand { get; }

        protected override string RepoName => SelectedRepository?.GetRealRepoName();

        public CsrCloneWindowViewModel(CommonDialogWindowBase owner) : base(owner)
        {
            OkCommand = new ProtectedCommand(taskHandler: () => ExecuteAsync(() => CloneAsync()), canExecuteCommand: false);
        }

        protected override async Task ListRepoAsync()
        {
            Debug.WriteLine("ListRepoAsync");

            Repositories = null;
            if (SelectedProject == null)
            {
                return;
            }

            Repositories = await CsrUtils.GetCloudReposAsync(SelectedProject.ProjectId);
            SelectedRepository = Repositories?.FirstOrDefault();
        }

        private Task CloneAsync() => CloneAsync(SelectedRepository);
    }
}
