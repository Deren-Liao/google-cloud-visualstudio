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
using StringResources = GoogleCloudExtension.Resources;
using GoogleCloudExtension.Theming;
using System.Collections.Generic;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// Dialog to Create a new Google Cloud Source Repository.
    /// </summary>
    public class CsrAddRepoWindow : CommonDialogWindowBase
    {
        private CsrAddRepoWindowViewModel ViewModel { get; }

        private CsrAddRepoWindow(IEnumerable<string> repoNameList, Project project)
            : base(StringResources.CsrAddRepoWindowTitle)
        {
            ViewModel = new CsrAddRepoWindowViewModel(this, repoNameList, project);
            Content = new CsrAddRepoWindowContent { DataContext = ViewModel };
        }

        /// <summary>
        /// For unit test only, don't call it in production code.
        /// </summary>
        internal CsrAddRepoWindow() : base(StringResources.CsrAddRepoWindowTitle) { }

        /// <summary>
        /// Create Google Cloud Source Repository.
        /// </summary>
        /// <param name="repoNameList">A list of existing repos.</param>
        /// <param name="project">The GCP project.</param>
        /// <returns>The new repo object.</returns>
        public static Repo PromptUser(IEnumerable<string> repoNameList, Project project)
        {
            var dialog = new CsrAddRepoWindow(repoNameList, project);
            dialog.ShowModal();
            return dialog.ViewModel.Result;
        }
    }
}
