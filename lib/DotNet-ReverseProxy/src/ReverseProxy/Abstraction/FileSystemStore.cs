// <copyright file="FileSystemStore.cs" company="Henrik Jensen">
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

[ExcludeFromCodeCoverage]
internal sealed class FileSystemStore : IFileStore
{
  public string CombinePath(string path1, string path2) => Path.Combine(path1, path2);

  public string GetFullPath(string path) => Path.GetFullPath(path);

  public bool FileExists(string? path) => File.Exists(path);

  public bool DirectoryExists(string? path) => Directory.Exists(path);

  public string ReadAllText(string path) => File.ReadAllText(path);

  public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

  public void WriteAllText(string path, string? contents) => File.WriteAllText(path, contents);
}
