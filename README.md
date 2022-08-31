# eide_tools

本处存放 eide 内部所使用的二进制程序

本项目使用 `VS2022` + `.NET6`

## 编译/安装项目

编译：

- 安装 `.NET6 SDK`

- Clone 仓库

- 在仓库目录下执行 `publish.bat` 脚本，成功后将生成到 `dist` 目录

安装：

- 将 `dist/win-xxx` 目录中的文件复制并覆盖到 `C:\Users\<用户名>\.eide\bin\builder\bin` 中去

---

# eide_tools

This repo is used to place eide internal executable binaries

This project use `VS2022` + `.NET6`

## Build/Install Project

Build:

- Install `.NET6 SDK`

- Clone this repo

- Execute `publish.bat` script in repo root folder, and product will be generated to `dist` folder

Install:

- Copy all files which are in `dist/win-xxx` folder and override them to `C:\Users\<USER_NAME>\.eide\bin\builder\bin` folder
