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

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// View model to <seealso cref="CsrSectionControl"/>.
    /// </summary>
    [Export(typeof(ISectionViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CsrSectionControlViewModel : ViewModelBase, ISectionViewModel
    {
        /// Sometimes, the view and view model is recreated by Team Explorer.
        /// This is to preserve the states when a new user control is created.
        private static string s_currentAccount;
        private static bool s_gitInited;

        private ITeamExplorerUtils _teamExplorerService;
        private readonly CsrReposContent _reposContent = new CsrReposContent();
        private readonly CsrUnconnectedContent _unconnectedContent = new CsrUnconnectedContent();
        private CsrReposViewModel _reposViewModel;
        private CsrUnconnectedViewModel _unconnectedViewModel;
        private CsrGitSetupViewModel _gitSetupViewModel;
        private ContentControl _content;
        private EventHandler _accountChangedHandler;

        /// <summary>
        /// The content for the section control.
        /// </summary>
        public ContentControl Content
        {
            get { return _content; }
            private set { SetValueAndRaise(ref _content, value); }
        }

        [ImportingConstructor]
        public CsrSectionControlViewModel()
        {
            Debug.WriteLine("new CsrSectionControlViewModel");
        }

        /// <summary>
        /// Display the unconnected view
        /// </summary>
        public void Disconnect()
        {
            Content = _unconnectedContent;
            s_currentAccount = null;
        }

        /// <summary>
        /// Switch to connected view
        /// </summary>
        public async Task Connect()
        {
            if (!await InitializeGit())
            {
                // TODO: Show error dialog
            }

            // Continue even if Initialize Git fails
            if (CredentialsStore.Default.CurrentAccount == null)
            {
                ManageAccountsWindow.PromptUser();
            }
            if (CredentialsStore.Default.CurrentAccount != null)
            {
                Content = _reposContent;
                Refresh();
            }
        }

        public void ContinueInitialize()
        {
            Debug.WriteLine("CsrSectionControlViewModel Initialize");
            
            _reposViewModel = new CsrReposViewModel(_teamExplorerService);
            _unconnectedViewModel = new CsrUnconnectedViewModel(this);
            _reposContent.DataContext = _reposViewModel;
            _unconnectedContent.DataContext = _unconnectedViewModel;

            _accountChangedHandler = (sender, e) => OnAccountChanged();
            CredentialsStore.Default.CurrentAccountChanged += _accountChangedHandler;
            CredentialsStore.Default.Reset += _accountChangedHandler;

            if (CredentialsStore.Default.CurrentAccount != null)
            {
                Content = _reposContent;
                if (s_currentAccount != CredentialsStore.Default.CurrentAccount?.AccountName)
                {
                    if (!SetGitCredential())
                    {
                        // TODO: Show error dialog.
                    }
                    // Continue nevertheless SetGitCredential succeeds or not.
                    _reposViewModel.Refresh();
                }
                s_currentAccount = CredentialsStore.Default.CurrentAccount?.AccountName;
            }
            else
            {
                Disconnect();
            }
        }

        #region implement interface ISectionViewModel

        /// <summary>
        /// Implicit implementation to ISectionViewModel.Refresh. 
        /// Using implicit declaration so that it can be accessed by 'this' object too.
        /// </summary>
        public void Refresh()
        {
            if (Content?.DataContext != null && Content.DataContext is CsrGitSetupViewModel)
            {
                if (_gitSetupViewModel.TestCommand.CanExecute(null))
                {
                    _gitSetupViewModel.TestCommand.Execute(null);
                }
                return;
            }

            Debug.WriteLine("CsrSectionControlViewModel.Refresh");
            if (CredentialsStore.Default.CurrentAccount == null)
            {
                Disconnect();
            }
            else
            {
                s_currentAccount = CredentialsStore.Default.CurrentAccount?.AccountName;
                _reposViewModel.Refresh();
            }
        }

        void ISectionViewModel.Initialize(ITeamExplorerUtils teamExplorerService)
        {
            _teamExplorerService = teamExplorerService.ThrowIfNull(nameof(teamExplorerService));
            Action continueInit = () =>
            {

            };

            if (!CsrGitSetupViewModel.GitInstallationVerified)
            {
                ErrorHandlerUtils.HandleAsyncExceptions(CheckInstallation);
            }
        }

        private async Task CheckInstallation()
        {
            await CsrGitSetupViewModel.CheckInstallation();
            if (!CsrGitSetupViewModel.GitInstallationVerified)
            {
                _gitSetupViewModel = new CsrGitSetupViewModel(this);
                CsrGitSetupContent content = new CsrGitSetupContent();
                content.DataContext = _gitSetupViewModel;
                Content = content;
            }
            else
            {
                ContinueInitialize();
            }
        }

        void ISectionViewModel.UpdateActiveRepo(string newRepoLocalPath)
        {
            Debug.WriteLine($"CsrSectionControlViewModel.UpdateActiveRepo {newRepoLocalPath}");
            _reposViewModel?.SetActiveRepo(newRepoLocalPath);
        }

        void ISectionViewModel.Cleanup()
        {
            if (_accountChangedHandler != null)
            {
                CredentialsStore.Default.Reset -= _accountChangedHandler;
                CredentialsStore.Default.CurrentAccountChanged -= _accountChangedHandler;
            }
        }

        #endregion

        private void OnAccountChanged()
        {
            if (CredentialsStore.Default.CurrentAccount != null)
            {
                if (!SetGitCredential())
                {
                    return;
                }
            }
            if (s_currentAccount == CredentialsStore.Default.CurrentAccount?.AccountName)
            {
                return;
            }
            Refresh();
        }

        private static async Task<bool> InitializeGit()
        {
            if (s_gitInited)
            {
                return true;
            }

            if (!await CsrGitUtils.SetUseHttpPath())
            {
                // TODO: show error message
                return false;
            }

            s_gitInited = SetGitCredential();
            return s_gitInited;
        }

        private static bool SetGitCredential()
        {
            if (CredentialsStore.Default.CurrentAccount != null)
            {
                if (!CsrGitUtils.StoreCredential(
                    "https://source.developers.google.com",
                    CredentialsStore.Default.CurrentAccount.RefreshToken,
                    useHttpPath: false))
                {
                    // TODO: show error message
                    return false;
                }
            }
            return true;
        }
    }
}
