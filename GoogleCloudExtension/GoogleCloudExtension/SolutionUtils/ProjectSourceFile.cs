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

using EnvDTE;
using System;
using System.Diagnostics;
using IOPath = System.IO.Path;
using System.Linq;

namespace GoogleCloudExtension.SolutionUtils
{
    /// <summary>
    /// This class represents a project source file. 
    /// Typlically .cs file.
    /// </summary>
    internal class ProjectSourceFile
    {
        private static readonly string[] s_supportedFileExtension = { ".cs" };

        private readonly ProjectHelper _owningProject;

        /// <summary>
        /// The <seealso cref="ProjectItem"/> object.
        /// </summary>
        public  readonly ProjectItem ProjectItem;

        /// <summary>
        /// The file path.
        /// </summary>
        public readonly string FullName;

        /// <summary>
        /// The path relative to the project root if <seealso cref="ProjectHelper.ProjectRoot"/> is valid.
        /// </summary>
        public readonly Lazy<string> RelativePath;

        /// <summary>
        /// Initializes an instance of <seealso cref="ProjectSourceFile"/> class.
        /// </summary>
        /// <param name="projectItem">A project item that is physical file.</param>
        /// <param name="project">The container project of type <seealso cref="ProjectHelper"/></param>
        private ProjectSourceFile(ProjectItem projectItem, ProjectHelper project)
        {
            ProjectItem = projectItem;
            FullName = ProjectItem.FileNames[0].ToLowerInvariant();
            _owningProject = project;
            RelativePath = new Lazy<string>(GetRelativePath);
        }

        /// <summary>
        /// Create a <seealso cref="ProjectSourceFile"/> object wrapping up a ProjectItem interface.
        /// Together with private constructor, this ensures object creation won't run into exception. 
        /// </summary>
        /// <param name="projectItem">A project item.</param>
        /// <param name="project">The container project of type <seealso cref="ProjectHelper"/></param>
        /// <returns>
        /// The created object.
        /// null: if the projectItem is null or the item is not physical source sfile.
        /// </returns>
        public static ProjectSourceFile Create(ProjectItem projectItem, ProjectHelper project)
        {
            if (!IsValidSupportedItem(projectItem) || project == null)
            {
                return null;
            }

            return new ProjectSourceFile(projectItem, project);
        }

        /// <summary>
        /// Verifies if a giving path match the source file item path.
        /// </summary>
        public bool IsMatchingPath(string filePath)
        {
            var path = filePath.ToLowerInvariant();
            return path.EndsWith(RelativePath.Value);
        }

        /// <summary>
        /// Get the project item path relative to the project root. 
        /// The relative path starts with '\' character.
        /// </summary>
        private string GetRelativePath()
        {
            if (_owningProject.ProjectRoot != null && FullName.StartsWith(_owningProject.ProjectRoot))
            {
                return FullName.Substring(_owningProject.ProjectRoot.Length);
            }
            else
            {
                // Fallback to compare the root of both paths.
                int baseIndex = 0;
                for (int i = 0; i < FullName.Length && i < _owningProject.FullName.Length && FullName[i] == _owningProject.FullName[i]; ++i)
                {
                    if (FullName[i] == IOPath.DirectorySeparatorChar)
                    {
                        baseIndex = i;
                    }
                }
                return FullName.Substring(baseIndex);
            }
        }

        private static bool IsValidSupportedItem(ProjectItem projectItem)
        {
            if (EnvDTE.Constants.vsProjectItemKindPhysicalFile != projectItem?.Kind ||
                !s_supportedFileExtension.Contains(IOPath.GetExtension(projectItem.Name).ToLower()))
            {
                return false;
            }

            if (projectItem.FileCount != 1)
            {
                Debug.WriteLine($"project item file count is {projectItem.FileCount}. Expects 1");
                return false;
            }

            return true;
        }
    }
}
