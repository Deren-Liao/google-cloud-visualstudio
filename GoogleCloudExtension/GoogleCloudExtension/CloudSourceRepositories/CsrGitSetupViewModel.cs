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

using GoogleCloudExtension.Accounts;
using GoogleCloudExtension.GitUtils;
using GoogleCloudExtension.ManageAccounts;
using GoogleCloudExtension.TeamExplorerExtension;
using GoogleCloudExtension.Utils;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// View model to CsrUnconnectedContent.xaml
    /// </summary>
    public class CsrGitSetupViewModel : ViewModelBase
    {
        private static string s_error;

        private readonly CsrSectionControlViewModel _parent;
        private bool _isEnabled = false;

        public bool IsEnable
        {
            get { return _isEnabled; }
            private set { SetValueAndRaise(ref _isEnabled, value);  }
        }

        public static bool GitInstallationVerified { get; private set; }

        public string ErrorMessage
        {
            get { return s_error; }
        }

        /// <summary>
        /// Respond to InstallGit button
        /// </summary>
        public ICommand InstallGitCommand { get; }

        /// <summary>
        /// Respond to Test installation button
        /// </summary>
        public ICommand TestCommand { get; }

        public CsrGitSetupViewModel(CsrSectionControlViewModel parent)
        {
            _parent = parent;
            InstallGitCommand = new ProtectedCommand(
                () => Process.Start(ValidateGitDependencyHelper.GitInstallationLink));
            TestCommand = new ProtectedCommand(taskHandler: OnTestRequest);
        }

        public async Task OnTestRequest()
        {
            IsEnable = false;
            try
            {
                await CheckInstallation();
                if (GitInstallationVerified)
                {
                    _parent.ContinueInitialize();
                }
                else
                {
                    RaisePropertyChanged(nameof(ErrorMessage));
                }
            }
            finally
            {
                IsEnable = true;
            }

        }

        public static async Task CheckInstallation()
        {
            if (GitInstallationVerified)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(GitRepository.GetGitPath()))
            {
                s_error = Resources.GitUtilsMissingGitErrorTitle;
                return;
            }
            if (!(await GitRepository.GitCredentialManagerInstalled()))
            {
                s_error = Resources.GitUtilsGitCredentialManagerNotInstalledMessage;
                return;
            }
            s_error = null;
            GitInstallationVerified = true;
        }

    }
}
