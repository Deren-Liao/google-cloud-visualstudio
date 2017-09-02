// Copyright 2016 Google Inc. All Rights Reserved.
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

using Google;
using Google.Apis.CloudFunctions.v1beta2;
using Google.Apis.CloudFunctions.v1beta2.Data;
using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GoogleCloudExtension.DataSources
{
    /// <summary>
    /// Data source for Google Cloud Functions
    /// </summary>
    public class GcfDataSource : DataSourceBase<CloudFunctionsService>
    {
        /// <summary>
        /// Initializes an instance of the data source.
        /// </summary>
        /// <param name="projectId">The project id that contains the GCF instances.</param>
        /// <param name="credential">The credentials to use for the call.</param>
        /// <param name="appName">The name of the application.</param>
        public GcfDataSource(string projectId, GoogleCredential credential, string appName)
            : base(projectId, credential, init => new CloudFunctionsService(init), appName)
        { }

        /// <summary>
        /// Get a list of Cloud Function
        /// </summary>
        /// <param name="location">
        /// refer to https://cloud.google.com/functions/docs/reference/rpc/google.cloud.location#google.cloud.location.Locations
        /// </param>
        /// <returns>A list of <seealso cref="CloudFunction"/></returns>
        public async Task<IList<CloudFunction>> ListFunctionsAsync(string location)
        {
            return await LoadPagedListAsync(
                (token) =>
                {
                    string gcfLocation = $"projects/{ProjectId}/locations/{location}";
                    var request = Service.Projects.Locations.Functions.List(gcfLocation);
                    request.PageToken = token;
                    return request.ExecuteAsync();
                },
                x => x.Functions,
                x => x.NextPageToken
                );
        }
    }
}
