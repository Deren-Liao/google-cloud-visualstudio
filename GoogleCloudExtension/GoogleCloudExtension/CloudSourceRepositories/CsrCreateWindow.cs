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

using GoogleCloudExtension.Theming;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// Dialog to create a new Google Cloud Source Repository.
    /// </summary>
    public class CsrCreateWindow : CommonDialogWindowBase
    {
        private  CsrCreateWindowViewModel ViewModel { get; }

        private CsrCreateWindow(): base("Create Google repository")
        {
            ViewModel = new CsrCreateWindowViewModel(this);
            Content = new CsrCreateWindowContent { DataContext = ViewModel };
        }

        /// <summary>
        /// Show create a new repository dialog
        /// </summary>
        /// <returns>A repo item shown in CSR section at Team Explorer Connect tab.</returns>
        public static RepoItemViewModel PromptUser()
        {
            var dialog = new CsrCreateWindow();
            dialog.ShowModal();
            return dialog.ViewModel.Result;
        }
    }
}
