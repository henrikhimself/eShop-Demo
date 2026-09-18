// <copyright file="IFileStore.cs" company="Henrik Jensen">
// Copyright 2025 Henrik Jensen
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace Hj.ReverseProxy.Abstraction;

internal interface IFileStore
{
  string CombinePath(string path1, string path2);

  string GetFullPath(string path);

  bool FileExists(string? path);

  bool DirectoryExists(string? path);

  string ReadAllText(string path);

  void WriteAllText(string path, string? contents);

  void WriteAllBytes(string path, byte[] bytes);
}
