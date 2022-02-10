using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace versoft.module_manager
{
    public class ModuleManagerWindow : EditorWindow
    {
        public string gitUserId;
        public string gitUserToken;
        public string repositoryToDownload;
        public string branchToDownload;
        private Dependencies _dependencies = new Dependencies();
        private Dependencies storedDependencies = new Dependencies();
        private Task runningTask = null;

        [MenuItem("Versoft/PackageManager/Open #p")]
        static void Init()
        {
            ModuleManagerWindow window = EditorWindow.GetWindow<ModuleManagerWindow>();
            window.gitUserId = PlayerPrefs.GetString(Const.UserNamePlayerPrefsKey, "");
            window.gitUserToken = PlayerPrefs.GetString(Const.UserTokenPlayerPrefsKey, "");
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Base Settings", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(runningTask != null);
            
            gitUserId = EditorGUILayout.TextField("Git User", gitUserId);
            gitUserToken = EditorGUILayout.PasswordField("Git User Token", gitUserToken);
            repositoryToDownload = EditorGUILayout.TextField("Package", repositoryToDownload);
            branchToDownload = EditorGUILayout.TextField("Branch", branchToDownload);

            if (GUILayout.Button("Get Package"))
            {
                runningTask = GetPackageFromGithub();
            }
            
            EditorGUI.EndDisabledGroup();
        }

        private async Task GetPackageFromGithub()
        {
            bool success = await CalculateDependencies(repositoryToDownload, branchToDownload);
            if (success)
            {
                // Get the packages from Github
                await GetPackageWithDependencies();

                // Update the persistent Dependencies dictionary
                UpdateDependenciesJson();

                // Save the Dependencies to the Json
                SaveDependenciesToJson();

                // Save the user data to make it easier for next time.
                PlayerPrefs.SetString(Const.UserNamePlayerPrefsKey, gitUserId);
                PlayerPrefs.SetString(Const.UserTokenPlayerPrefsKey, gitUserToken);

                // Refresh the Database
                AssetDatabase.Refresh();
            }
            runningTask = null;
        }

        private async Task<bool> CalculateDependencies(string package, string branch)
        {
            LoadDependenciesFromJson();
            storedDependencies = new Dependencies();
            bool changes = false;
            try
            {
                await CalculateDependenciesInternal(package, branch);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
                storedDependencies.dependencies.Clear();
            }
            finally
            {
                changes = storedDependencies.dependencies != null && storedDependencies.dependencies.Count > 0;

            }
            return changes;
        }

        private async Task CalculateDependenciesInternal(string package, string branch)
        {
            // First we store it in the dependencies.
            storedDependencies.dependencies.Add(package, branch);

            // Then we check the dependencies of this package.
            var dependencies = await GetPackageDependencies(package, branch);
            if (dependencies != null && dependencies.dependencies != null)
            {
                foreach (var pair in dependencies.dependencies)
                {
                    if (storedDependencies.dependencies.ContainsKey(pair.Key) && storedDependencies.dependencies[pair.Key] != pair.Value)
                    {
                        throw new System.Exception("Invalid Dependencies");
                    }
                    else if (!storedDependencies.dependencies.ContainsKey(pair.Key))
                    {
                        await CalculateDependenciesInternal(pair.Key, pair.Value);
                    }
                }
            }

        }

        private async Task GetPackageWithDependencies()
        {
            if (storedDependencies.dependencies != null)
            {
                List<Package> packages = new List<Package>();
                foreach (var pair in storedDependencies.dependencies)
                {
                    if (!_dependencies.dependencies.ContainsKey(pair.Key))
                    {
                        Package package = new Package
                        {
                            Repository = pair.Key,
                            Branch = pair.Value
                        };
                        packages.Add(package);
                    }
                }

                await DownloadFiles.DownloadZipArchieveAsync(gitUserId, gitUserToken, packages);
            }
        }

        private void UpdateDependenciesJson()
        {
            if (storedDependencies.dependencies != null)
            {
                foreach (var pair in storedDependencies.dependencies)
                {
                    if (!_dependencies.dependencies.ContainsKey(pair.Key))
                    {
                        _dependencies.dependencies.Add(pair.Key, pair.Value);
                    }
                }
            }
        }

        private void SaveDependenciesToJson()
        {
            if (_dependencies == null) { return; }
            var stream = File.Open(Path.Combine(Application.dataPath, Const.DependenciesFolder, Const.DependenciesFileName), FileMode.Create);
            string parsedJson = JsonConvert.SerializeObject(_dependencies, Formatting.Indented);
            stream.AddText(parsedJson);
            stream.Close();
        }

        private void LoadDependenciesFromJson()
        {
            try
            {
                string directory = Path.Combine(Application.dataPath, Const.DependenciesFolder);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }


                FileStream fileStream = File.Open(Path.Combine(directory, Const.DependenciesFileName), FileMode.OpenOrCreate);
                var stream = new StreamReader(fileStream);
                string json = stream.ReadToEnd();
                _dependencies = JsonConvert.DeserializeObject<Dependencies>(json);

                // This is here in case there's no file yet. We can't have a null dictionary or else it will blow up later.
                if (_dependencies == null)
                {
                    _dependencies = new Dependencies();
                }

                stream.Close();
                fileStream.Close();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
                runningTask = null;
            }
        }

        private async Task<Dependencies> GetPackageDependencies(string package, string branch)
        {
            var dependencies = await DownloadFiles.DownloadDependencyFile(gitUserId, gitUserToken, package, branch);
            return dependencies;
        }
    }
}
