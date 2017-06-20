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

using StringResources = GoogleCloudExtension.Resources;
using GoogleCloudExtension.Theming;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// Dialog to clone Google Cloud Source Repository.
    /// </summary>
    public class CsrCloneWindow : CommonDialogWindowBase
    {
        private  CsrCloneCreateViewModelBase ViewModel { get; }

        private CsrCloneWindow(): base(StringResources.CsrCloneWindowTitle)
        {
            ViewModel = new CsrCloneWindowViewModel(this);
            Content = new CsrCloneWindowContent { DataContext = ViewModel };
        }

        /// <summary>
        /// Clone a repository from Google Cloud Source Repository.
        /// </summary>
        /// <returns>The cloned repo item.</returns>
        public static RepoItemViewModel PromptUser()
        {
            var dialog = new CsrCloneWindow();
            dialog.ShowModal();
            return dialog.ViewModel.Result;
        }
    }
}