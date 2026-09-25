@echo off
cd /d "%~dp0"
start "" "LLMBenchmark.exe" -gpu 99 -suites A,B,D,H,J
