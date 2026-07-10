# CyreneJson

一个轻量级构建时 JSON 序列化上下文生成器，基于 Roslyn 分析自动生成 `System.Text.Json` 的 `JsonSerializerContext` 和多态类型配置。

[![NuGet](https://img.shields.io/nuget/v/CyreneJson.BuildTask.svg)](https://www.nuget.org/packages/CyreneJson.BuildTask/)
[![License](https://img.shields.io/github/license/CyreneLaboratory/CyreneJson)](https://github.com/CyreneLaboratory/CyreneJson/blob/main/LICENSE.txt)

## 特性

- **自动生成** - 构建时自动扫描标记类型，生成 `JsonSerializerContext`
- **多态支持** - 自动发现派生类型并生成 `JsonPolymorphic` / `JsonDerivedType` 配置
- **自定义集合** - 支持通过 `[CyreneHandler]` 注册自定义集合处理器
- **AOT 兼容** - 生成的代码完全支持 Native AOT 编译

## 安装

```bash
dotnet add package CyreneJson.BuildTask
```

## 快速开始

### 基本用法

使用 `[CyreneJson]` 标记需要序列化的类型：

```csharp
using CyreneJson.Attributes;

[CyreneJson]
public class UserInfo
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Tags { get; set; }
}
```

构建时会自动生成 `CyreneJsonContext`，包含所有标记类型及其属性引用的类型。

### 多态类型

标记基类后，派生类型会被自动发现并生成多态配置：

```csharp
[CyreneJson]
public partial class Animal
{
    public string Name { get; set; }
}

public class Cat : Animal
{
    public bool Indoor { get; set; }
}

public class Dog : Animal
{
    public string Breed { get; set; }
}
```

### 自定义集合处理器

```csharp
using CyreneJson.Attributes;

[CyreneHandler(typeof(MyCollection<>))]
public class MyCollectionHandler;
```

## 要求

- .NET 8.0 或更高版本

## 许可证

本项目采用 [MIT](LICENSE.txt)。

## 链接

- [NuGet](https://www.nuget.org/packages/CyreneJson.BuildTask/)
- [GitHub](https://github.com/CyreneLaboratory/CyreneJson)
- [作者](https://github.com/Letheriver2007)
