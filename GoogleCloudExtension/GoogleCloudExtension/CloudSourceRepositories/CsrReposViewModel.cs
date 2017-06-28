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
using GoogleCloudExtension.DataSources;
using GoogleCloudExtension.GitUtils;
using GoogleCloudExtension.TeamExplorerExtension;
using GoogleCloudExtension.Utils;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GoogleCloudExtension.CloudSourceRepositories
{
    /// <summary>
    /// View model to CsrReposContent.xaml
    /// </summary>
    public class CsrReposViewModel : ViewModelBase
    {
        /// <summary>
        /// Sometimes, the view and view model is recreated by Team Explorer.
        /// This is to preserve the list when a new user control is created.
        /// Without doing so, user constantly sees the list of repos are loading without reasons.
        /// </summary>
        private static ObservableCollection<RepoItemViewModel> s_repoList;
        private static RepoItemViewModel s_activeRepo;

        private readonly ITeamExplorerUtils _teamExplorer;
        private bool _isReady = true;
        private RepoItemViewModel _selectedRepo;

        /// <summary>
        /// Gets the current active Repo
        /// </summary>
        public static RepoItemViewModel ActiveRepo
        {
            get { return s_activeRepo; }
            set
            {
                if (value != s_activeRepo && s_activeRepo != null)
                {
                    s_activeRepo.IsActiveRepo = false;
                }

                if (value != null)
                {
                    value.IsActiveRepo = true;
                }

                s_activeRepo = value;
            }
        }

        /// <summary>
        /// Indicates if the current view is not busy.
        /// </summary>
        public bool IsReady
        {
            get { return _isReady; }
            private set { SetValueAndRaise(ref _isReady, value); }
        }

        /// <summary>
        /// Show the list of repositories
        /// </summary>
        public ObservableCollection<RepoItemViewModel> Repositories
        {
            get { return s_repoList; }
            private set { SetValueAndRaise(ref s_repoList, value); }
        }

        /// <summary>
        /// Currently selected repository
        /// </summary>
        public RepoItemViewModel SelectedRepository
        {
            get { return _selectedRepo; }
            set { SetValueAndRaise(ref _selectedRepo, value); }
        }

        /// <summary>
        /// Responds to Clone command
        /// </summary>
        public ICommand CloneCreateRepoCommand { get; }

        /// <summary>
        /// Responds to Disconnect command
        /// </summary>
        public ICommand DisconnectCommand { get; }

        /// <summary>
        /// Responds to list view double click event
        /// </summary>
        public ICommand ListDoubleClickCommand { get; }

        public CsrReposViewModel(ITeamExplorerUtils teamExplorer)
        {
            _teamExplorer = teamExplorer.ThrowIfNull(nameof(teamExplorer));
            ListDoubleClickCommand = new ProtectedCommand(() =>
            {
                SetRepoActive(SelectedRepository);
                _teamExplorer.ShowHomeSection();
            });
            CloneCreateRepoCommand = new ProtectedAsyncCommand(CloneCreateRepoAsync);
        }

        /// <summary>
        /// Reload the repository list.
        /// </summary>
        public void Refresh()
        {
            ErrorHandlerUtils.HandleAsyncExceptions(ListRepositoryAsync);
        }

        /// <summary>
        /// When user double clicks at a repository, set it as active repo.
        /// </summary>
        public void SetRepoActive(RepoItemViewModel repo)
        {
            if (repo?.IsActiveRepo == false)
            {
                SetCurrentRepo(repo.LocalPath);

                // Note, the order is critical.
                // When switching to HomeSection, current "this" object is destroyed.
                ActiveRepo = repo;
            }
        }

        /// <summary>
        /// Set a repository as active, show the item in Bold font.
        /// </summary>
        /// <param name="localPath">The repository local path</param>
        public void ShowActiveRepo(string localPath)
        {
            var repoItem = Repositories?.FirstOrDefault(
                x => String.Compare(x.LocalPath, localPath, StringComparison.OrdinalIgnoreCase) == 0);
            ActiveRepo = repoItem;
        }

        private void SetCurrentRepo(string localPath)
        {
            string guid = Guid.NewGuid().ToString();
            try
            {
                ShellUtils.CreateEmptySolution(localPath, guid);
            }
            finally
            {
                try
                {
                    // Clean up the dummy `.vs` directory.
                    string tmpPath = Path.Combine(localPath, ".vs", guid);
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, recursive: true);
                    }
                }
                catch (Exception ex) when (
                    ex is IOException ||
                    ex is UnauthorizedAccessException)
                { }
            }
        }


        /// <summary>
        /// Get a list of local repositories.  It is saved to local variable localRepos.
        /// For each local repository, get remote urls list.
        /// From each URL, get the project-id. 
        /// Now, check if the list of 'cloud repositories' under the project-id contains the URL.
        /// If it does, the local repository with the URL will be shown to user.
        /// </summary>
        private async Task ListRepositoryAsync()
        {
            if (!IsReady)
            {
                return;
            }

            // GetProjectsAsync set/reset IsReady, put it before the following IsReady flag
            var projects = await GetProjectsAsync();

            IsReady = false;
            Repositories = new ObservableCollection<RepoItemViewModel>();
            try
            {
                await AddLocalReposAsync(await GetLocalGitRepositories(), projects);

                ShowActiveRepo(_teamExplorer.GetActiveRepository());
            }
            finally
            {
                IsReady = true;
            }
        }

        /// <summary>
        /// projectRepos is used to cache the list of 'cloud repos' of the project-id.
        /// </summary>
        private async Task AddLocalReposAsync(IList<GitRepository> localRepos, IList<Project> projects)
        {
            Dictionary<string, IEnumerable<Repo>> projectRepos
                = new Dictionary<string, IEnumerable<Repo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var localGitRepo in localRepos)
            {
                IList<string> remoteUrls = await localGitRepo.GetRemotesUrls();
                foreach (var url in remoteUrls)
                {
                    string projectId = CsrUtils.ParseProjectId(url);
                    if (String.IsNullOrWhiteSpace(projectId) ||
                        !projects.Any(x => x.ProjectId == projectId))
                    {
                        continue;
                    }

                    var cloudRepo = await TryGetCloudRepoAsync(url, projectId, projectRepos);
                    if (cloudRepo == null)
                    {
                        Debug.WriteLine($"{projectId} repos does not contain {url}");
                        continue;
                    }
                    Repositories.Add(new RepoItemViewModel(cloudRepo, localGitRepo.Root));
                    break;
                }
            }
        }

        private async Task<Repo> TryGetCloudRepoAsync(
            string url, 
            string projectId, 
            Dictionary<string, IEnumerable<Repo>> projectReposMap)
        {
            IEnumerable<Repo> cloudRepos;
            Debug.WriteLine($"Check project id {projectId}");
            if (!projectReposMap.TryGetValue(projectId, out cloudRepos))
            {
                try
                {
                    cloudRepos = await CsrUtils.GetCloudReposAsync(projectId);
                }
                catch (DataSourceException)
                {
                    _teamExplorer.ShowMessage(
                        $"Failed to get repos for GCP project {projectId}",
                        null);
                    cloudRepos = null;
                }
                projectReposMap.Add(projectId, cloudRepos);
            }

            if (cloudRepos == null || !cloudRepos.Any())
            {
                Debug.WriteLine($"{projectId} has no repos found");
                return null;
            }

            return cloudRepos.FirstOrDefault(
                x => String.Compare(x.Url, url, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// The list of local git repositories that Visual Studio remembers.
        /// </summary>
        /// <returns>
        /// A list of local repositories.
        /// Empty list is returned, never return null.
        /// </returns>
        private async Task<List<GitRepository>> GetLocalGitRepositories()
        {
            List<GitRepository> localRepos = new List<GitRepository>();
            var repos = VsGitData.GetLocalRepositories(GoogleCloudExtensionPackage.VsVersion);
            if (repos != null)
            {
                var localRepoTasks = repos.Where(r => !string.IsNullOrWhiteSpace(r))
                        .Select(GitRepository.GetGitCommandWrapperForPathAsync);
                localRepos.AddRange((await Task.WhenAll(localRepoTasks)).Where(r => r != null));
            }
            return localRepos;
        }

        private async Task CloneCreateRepoAsync()
        {
            var projects = await GetProjectsAsync();
            if (!projects.Any())
            {
                return;
            }

            var result = CsrCloneWindow.PromptUser(projects);
            if (result != null)
            {
                var repoItem = result.RepoItem;
                if (Repositories == null)
                {
                    Repositories = new ObservableCollection<RepoItemViewModel>();
                }
                Repositories.Add(repoItem);

                // Created a new repo and cloned locally
                if (result.JustCreatedRepo)
                {
                    SetRepoActive(repoItem);

                    var msg = string.Format("The repository {0} has been created successfully.", repoItem.Name);
                    msg += " " + string.Format("[Create a new project or solution]({0}) now.", repoItem.LocalPath);

                    _teamExplorer.ShowMessage(msg,
                    command: new ProtectedCommand(handler: () =>
                    {
                        SetDefaultProjectPath(repoItem.LocalPath);
                        var serviceProvider = ShellUtils.GetGloblalServiceProvider();
                        var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
                        solution?.CreateNewProjectViaDlg(null, null, 0);
                        _teamExplorer.ShowHomeSection();
                    }));
                }
                else
                {
                    var msg = string.Format("The repository {0} has been cloned successfully.", repoItem.Name);
                    msg += " " + string.Format("[Switch to the repo]({0}) now.", repoItem.LocalPath);

                    _teamExplorer.ShowMessage(msg,
                    command: new ProtectedCommand(handler: () =>
                    {
                        SetRepoActive(repoItem);
                        _teamExplorer.ShowHomeSection();
                    }));
                }
            }
        }

        const string NewProjectDialogKeyPath = @"Software\Microsoft\VisualStudio\14.0\NewProjectDialog";
        const string MRUKeyPath = "MRUSettingsLocalProjectLocationEntries";
        internal static string SetDefaultProjectPath(string path)
        {
            var old = String.Empty;
            try
            {
                var newProjectKey = Registry.CurrentUser.OpenSubKey(NewProjectDialogKeyPath, true) ??
                    Registry.CurrentUser.CreateSubKey(NewProjectDialogKeyPath);
                Debug.Assert(newProjectKey != null, string.Format(CultureInfo.CurrentCulture,
                    "Could not open or create registry key '{0}'", NewProjectDialogKeyPath));

                using (newProjectKey)
                {
                    var mruKey = newProjectKey.OpenSubKey(MRUKeyPath, true) ?? Registry.CurrentUser.CreateSubKey(MRUKeyPath);
                    Debug.Assert(mruKey != null, string.Format(CultureInfo.CurrentCulture,
                        "Could not open or create registry key '{0}'", MRUKeyPath));

                    using (mruKey)
                    {
                        // is this already the default path? bail
                        old = (string)mruKey.GetValue("Value0", string.Empty,
                            RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (String.Equals(path.TrimEnd('\\'), old.TrimEnd('\\'),
                            StringComparison.CurrentCultureIgnoreCase))
                            return old;

                        // grab the existing list of recent paths, throwing away the last one
                        var numEntries = (int)mruKey.GetValue("MaximumEntries", 5);
                        var entries = new List<string>(numEntries);
                        for (int i = 0; i < numEntries - 1; i++)
                        {
                            var val = (string)mruKey.GetValue("Value" + i, String.Empty,
                                RegistryValueOptions.DoNotExpandEnvironmentNames);
                            if (!String.IsNullOrEmpty(val))
                                entries.Add(val);
                        }

                        newProjectKey.SetValue("LastUsedNewProjectPath", path);
                        mruKey.SetValue("Value0", path);
                        // bump list of recent paths one entry down
                        for (int i = 0; i < entries.Count; i++)
                            mruKey.SetValue("Value" + (i + 1), entries[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                //VsOutputLogger.WriteLine(string.Format(CultureInfo.CurrentCulture, "Error setting the create project path in the registry '{0}'", ex));
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture,
                    "Error setting the create project path in the registry '{0}'", ex));
            }
            return old;
        }

        /// <summary>
        /// Return a list of projects. Returns empty list if no item is found.
        /// </summary>
        private async Task<IList<Project>> GetProjectsAsync()
        {
            ResourceManagerDataSource resourceManager = DataSourceFactories.CreateResourceManagerDataSource();
            if (resourceManager == null)
            {
                return new List<Project>();
            }

            IsReady = false;
            try
            {
                var projects = await resourceManager.GetSortedActiveProjectsAsync();
                if (!projects.Any())
                {
                    UserPromptUtils.OkPrompt(
                        message: Resources.CsrNoProjectMessage,
                        title: Resources.CsrConnectSectionTitle);
                }
                return projects;
            }
            finally
            {
                IsReady = true;
            }
        }
    }
}
