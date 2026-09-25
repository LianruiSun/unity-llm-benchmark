using System.IO;
using System.Text.RegularExpressions;
using LLMUnity;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bench
{
    /// <summary>
    /// 项目里唯一的场景：Benchmark。
    /// 打开/编译项目时会自动生成（不需要手动点菜单）；模型、Build Settings 也自动处理。
    /// 另外提供一键打包给别人测试的菜单。
    /// </summary>
    [InitializeOnLoad]
    public static class BenchmarkSceneBuilder
    {
        const string RootDir = "Assets/Benchmark";
        const string ScenePath = RootDir + "/Benchmark.unity";
        const string BuildDir = "Builds/Benchmark";
        const string ExeName = "LLMBenchmark.exe";

        const string DefaultModelUrl =
            "https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/Qwen3.5-2B-Q4_K_M.gguf";
        const string DefaultModelLabel = "Qwen 3.5 2B";

        static bool busy;

        static BenchmarkSceneBuilder()
        {
            EditorApplication.delayCall += AutoEnsure;
        }

        // ------------------------------------------------------------------
        // 自动：项目一打开/一编译完，就保证场景、模型、Build Settings 都是对的
        // ------------------------------------------------------------------
        static void AutoEnsure()
        {
            if (busy || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                if (!File.Exists(ScenePath))
                {
                    BuildScene(true);
                    return;
                }

                if (!SessionState.GetBool("Bench.Repaired", false) && SceneModelIsEmpty() && FindModel() != null)
                {
                    SessionState.SetBool("Bench.Repaired", true);
                    BuildScene(true);
                    return;
                }
                EnsureBuildSettings();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Benchmark] 自动准备场景失败：" + e.Message);
            }
        }

        static bool SceneModelIsEmpty()
        {
            try
            {
                string text = File.ReadAllText(ScenePath);
                Match m = Regex.Match(text, @"^\s*_model:[ \t]*(.*)$", RegexOptions.Multiline);
                return m.Success && string.IsNullOrWhiteSpace(m.Groups[1].Value.Trim().Trim('"', '\''));
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------
        // 菜单
        // ------------------------------------------------------------------
        [MenuItem("Tools/Benchmark/Rebuild Scene")]
        public static void RebuildMenu() { BuildScene(false); }

        [MenuItem("Tools/Benchmark/Build Windows Player (给别人测试)")]
        public static void BuildPlayerMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("请先退出 Play 模式再打包。");
                return;
            }
            if (!File.Exists(ScenePath)) BuildScene(true);
            EnsureBuildSettings();
            if (FindModel() == null)
            {
                EditorUtility.DisplayDialog("还没有模型",
                    "打包前需要先准备模型：\n• Tools > Benchmark > Download Default Model\n• 或 Tools > Benchmark > Use Local .gguf Model...", "知道了");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, BuildDir);
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, ExeName);

            BuildPlayerOptions opt = new BuildPlayerOptions();
            opt.scenes = new[] { ScenePath };
            opt.locationPathName = exe;
            opt.target = BuildTarget.StandaloneWindows64;
            opt.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(opt);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Benchmark] 打包失败：" + report.summary.result + "，详见 Console 里的报错。");
                EditorUtility.DisplayDialog("打包失败", "结果：" + report.summary.result + "\n请查看 Console。", "好");
                return;
            }

            string modelName = FindModel();
            string modelSource = LLM.GetLLMManagerAssetRuntime(modelName);
            if (string.IsNullOrEmpty(modelSource) || !File.Exists(modelSource))
            {
                Debug.LogError("[Benchmark] 打包后找不到模型源文件：" + modelSource);
                EditorUtility.DisplayDialog("打包不完整", "模型文件未找到，无法生成可运行的测试包。", "好");
                return;
            }
            string streamingDir = Path.Combine(outDir, "LLMBenchmark_Data", "StreamingAssets");
            Directory.CreateDirectory(streamingDir);
            string modelTarget = Path.Combine(streamingDir, Path.GetFileName(modelName));
            if (!string.Equals(Path.GetFullPath(modelSource), Path.GetFullPath(modelTarget), System.StringComparison.OrdinalIgnoreCase))
                File.Copy(modelSource, modelTarget, true);
            if (new FileInfo(modelTarget).Length != new FileInfo(modelSource).Length)
            {
                Debug.LogError("[Benchmark] 模型复制不完整：" + modelTarget);
                EditorUtility.DisplayDialog("打包不完整", "模型复制不完整，请检查磁盘空间。", "好");
                return;
            }

            WriteLaunchers(outDir);
            Debug.Log("[Benchmark] 打包完成：" + outDir + "\n把整个文件夹压缩发给测试的人即可。");
            EditorUtility.RevealInFinder(exe);
        }

        [MenuItem("Tools/Benchmark/Open Log Folder")]
        public static void OpenLogFolder()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BenchmarkLogs");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("Tools/Benchmark/Download Default Model (Qwen 3.5 2B)")]
        public static async void DownloadDefaultModel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("请先退出 Play 模式再下载模型。");
                return;
            }
            EditorApplication.CallbackFunction tick = () => EditorUtility.DisplayProgressBar("下载模型", DefaultModelLabel + "（Q4_K_M，约 1.2 GB）", LLMManager.modelProgress);

            string filename = null;
            EditorApplication.update += tick;
            try { filename = await LLMManager.DownloadModel(DefaultModelUrl, true, DefaultModelLabel); }
            finally { EditorApplication.update -= tick; EditorUtility.ClearProgressBar(); }

            if (string.IsNullOrEmpty(filename))
            {
                Debug.LogError("[Benchmark] 模型下载失败（网络无法访问 huggingface.co？）。可手动下载 .gguf 后用 “Use Local .gguf Model...” 载入。");
                return;
            }
            BuildScene(true);
        }

        [MenuItem("Tools/Benchmark/Use Local .gguf Model...")]
        public static void UseLocalModel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("请先退出 Play 模式再选择模型。");
                return;
            }
            string path = EditorUtility.OpenFilePanel("选择 .gguf 模型文件", "", "gguf");
            if (string.IsNullOrEmpty(path)) return;
            string filename = LLMManager.LoadModel(path, true);
            if (string.IsNullOrEmpty(filename)) { Debug.LogError("[Benchmark] 载入模型失败。"); return; }
            BuildScene(true);
        }

        // ------------------------------------------------------------------
        // 生成场景
        // ------------------------------------------------------------------
        static void BuildScene(bool silent)
        {
            if (busy) return;
            busy = true;
            try
            {
                if (!AssetDatabase.IsValidFolder(RootDir)) AssetDatabase.CreateFolder("Assets", "Benchmark");

                // 当前打开的场景如果是从没存过盘的 Untitled 场景，不能用 Additive 叠加新场景
                // （Unity 会报 "Cannot create a new scene additively with an untitled scene unsaved"）。
                // 这种情况下改用 Single：反正没什么可保留的，下面会把结果存盘再重新打开。
                bool activeUntitled = string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
                NewSceneMode createMode = activeUntitled ? NewSceneMode.Single : NewSceneMode.Additive;
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, createMode);

                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camGo.AddComponent<AudioListener>();

                GameObject llmGo = new GameObject("LLM");
                LLM llm = llmGo.AddComponent<LLM>();
                llm.dontDestroyOnLoad = false;

                GameObject agentGo = new GameObject("LLMAgent");
                LLMAgent agent = agentGo.AddComponent<LLMAgent>();
                agent.llm = llm;
                agent.numPredict = 80;
                agent.temperature = 0f;
                agent.systemPrompt = "（运行时由 BenchmarkRunner 设置）";

                GameObject runnerGo = new GameObject("BenchmarkRunner");
                BenchmarkRunner runner = runnerGo.AddComponent<BenchmarkRunner>();
                runner.llm = llm;
                runner.agent = agent;

                bool hasModel = AssignModel(llm);
                llm.numGPULayers = 0;
                llm.contextSize = 1024;

                SceneManager.MoveGameObjectToScene(camGo, scene);
                SceneManager.MoveGameObjectToScene(llmGo, scene);
                SceneManager.MoveGameObjectToScene(agentGo, scene);
                SceneManager.MoveGameObjectToScene(runnerGo, scene);

                EditorUtility.SetDirty(llm);
                EditorUtility.SetDirty(agent);
                EditorUtility.SetDirty(runner);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();

                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                EnsureBuildSettings();

                if (!AnySceneDirty()) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                if (hasModel)
                    Debug.Log("[Benchmark] 场景已就绪：" + ScenePath + "，模型：" + llm.model + "。点 Play 即自动测试。");
                else
                {
                    Debug.LogWarning("[Benchmark] 场景已就绪，但还没有模型。请用 Tools > Benchmark > Download Default Model 或 Use Local .gguf Model...，场景会自动更新。");
                    if (!silent)
                        EditorUtility.DisplayDialog("还没有模型", "请用 Tools > Benchmark 里的 Download / Use Local 准备模型。", "知道了");
                }
            }
            finally { busy = false; }
        }

        static bool AnySceneDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
        }

        static string FindModel()
        {
            foreach (ModelEntry entry in LLMManager.modelEntries)
            {
                if (entry.lora || entry.embeddingOnly) continue;
                if (File.Exists(LLM.GetLLMManagerAssetRuntime(entry.filename))) return entry.filename;
            }
            // Fresh clones do not have the original developer's LLMManager PlayerPrefs.
            // The model committed in StreamingAssets must work without local setup.
            const string bundledModel = "Qwen3.5-2B-Q4_K_M.gguf";
            if (File.Exists(Path.Combine(Application.streamingAssetsPath, bundledModel))) return bundledModel;
            return null;
        }

        static bool AssignModel(LLM llm)
        {
            string name = FindModel();
            if (name == null) return false;
            llm.SetModel(name);
            return !string.IsNullOrEmpty(llm.model);
        }

        // ------------------------------------------------------------------
        // Build Settings：只保留这一个场景；顺便设好窗口/后台运行
        // ------------------------------------------------------------------
        static void EnsureBuildSettings()
        {
            if (!File.Exists(ScenePath)) return;
            EditorBuildSettingsScene[] want = { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettingsScene[] have = EditorBuildSettings.scenes;
            bool same = have.Length == 1 && have[0].path == ScenePath && have[0].enabled;
            if (!same) EditorBuildSettings.scenes = want;

            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
        }

        // ------------------------------------------------------------------
        // 打包后写启动脚本和说明
        // ------------------------------------------------------------------
        static void WriteLaunchers(string outDir)
        {
            foreach (string oldName in new[] { "1_运行测试_全部套件.bat", "2_运行测试_只测性能和结构化输出.bat", "3_运行测试_GPU加速.bat" })
            {
                string oldPath = Path.Combine(outDir, oldName);
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }
            WriteBat(outDir, "运行测试_GPU加速.bat", "-gpu 99 -suites A,B,D,H,J");

            string readme =
                "本地 LLM 性能测试\r\n" +
                "==========================\r\n\r\n" +
                "怎么测：\r\n" +
                "  1. 普通 CPU 测试：直接双击 " + ExeName + "。\r\n" +
                "     GPU 加速测试：双击 “运行测试_GPU加速.bat”。两者都运行 A/B/D/H/J 短输入用例。\r\n" +
                "  2. 程序会自动加载模型并连续测试，界面会显示进度。测试期间请不要运行其它吃 CPU/GPU 的程序，电脑接通电源。\r\n" +
                "  3. 测完后界面会显示结论。按 Enter 打开日志文件夹（BenchmarkLogs），按 Esc 退出。\r\n" +
                "  4. 查看 BenchmarkLogs 里最新的 .html 报告；需要分析细节时提供同名 .json。\r\n\r\n" +
                "说明：\r\n" +
                "  - 日志里只有硬件型号（CPU/GPU/内存）和测试数据，没有你的用户名、机器名或文件。\r\n" +
                "  - 中途按 Esc 可以中止，已经测完的部分也会保存。\r\n" +
                "  - 加载失败时也会生成日志，请一并发回。\r\n" +
                "  - 报告会显示 GPU 卸载层数与实际推理后端：CPU 测试为 0 层，GPU 测试为 99 层。若 GPU 后端未启用，GPU 测试会停止并报错。\r\n" +
                "  - 想改测试内容：把 BenchmarkSuite.json 放到本文件夹（和 " + ExeName + " 同一目录），程序会优先用它而不是内置的那份。\r\n";
            File.WriteAllText(Path.Combine(outDir, "说明.txt"), readme, new System.Text.UTF8Encoding(true));
        }

        static void WriteBat(string dir, string name, string args)
        {
            string content = "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"" + ExeName + "\" " + args + "\r\n";
            File.WriteAllText(Path.Combine(dir, name), content, new System.Text.UTF8Encoding(false));
        }
    }
}
