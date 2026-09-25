# Unity LLM Benchmark

本仓库包含 Unity 项目、Qwen3.5-2B-Q4_K_M 模型，以及可直接运行的 Windows build。

## 获取与打开项目

1. 安装 Git LFS，并在克隆后运行 `git lfs pull`。模型和原生运行库使用 LFS 保存；普通 ZIP 源码下载可能不包含这些文件的实际内容。
2. 用 Unity **6000.5.9f1** 打开仓库根目录。首次打开时，Unity Package Manager 需要联网获取 `ai.undream.llm` 等依赖。
3. 打开 `Assets/Benchmark/Benchmark.unity`，点击 Play。模型已放在 `Assets/StreamingAssets`，不需要从原开发电脑的用户目录复制文件。

## 直接运行 Windows build

完整 build 在 `builds/Benchmark`，请保留该目录中的所有文件：

- 双击 `LLMBenchmark.exe`：CPU 测试。
- 双击 `运行测试_GPU加速.bat`：GPU 加速测试。

两种启动方式都使用 A/B/D/H/J 短输入用例。报告写入 build 目录下的 `BenchmarkLogs`，其中会记录 GPU 卸载层数和实际推理后端。仓库不收录本地测试报告和 `test.zip`。

## 模型来源

模型文件为 [Unsloth 的 Qwen3.5-2B-Q4_K_M GGUF](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/blob/main/Qwen3.5-2B-Q4_K_M.gguf)，模型页面标示为 Apache-2.0 许可，许可文本见 `LICENSES/Apache-2.0.txt`。项目所用的 LLMUnity 版本固定在 `Packages/manifest.json` 中。
