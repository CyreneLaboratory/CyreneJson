# CyreneJson

一个轻量级构建时 JSON 序列化上下文生成器，基于 Roslyn 分析自动生成 `System.Text.Json` 的 `JsonSerializerContext` 和多态类型配置。

[![NuGet](https://img.shields.io/nuget/v/CyreneJson.BuildTask.svg)](https://www.nuget.org/packages/CyreneJson.BuildTask/)
[![License](https://img.shields.io/github/license/CyreneLaboratory/CyreneJson)](https://github.com/CyreneLaboratory/CyreneJson/blob/main/LICENSE.txt)

## 特性

- **自动扫描** - 扫描 `JsonSerializer.Serialize/Deserialize` 调用点，自动推断需要注册的类型
- **集中注册** - 通过 `[CyreneEntry(typeof(T))]` 在标记类上集中声明序列化根类型
- **多态支持** - 自动发现派生类型并生成 `JsonPolymorphic` / `JsonDerivedType` 配置
- **自定义集合** - 支持通过 `[CyreneHandler]` 注册自定义集合处理器
- **AOT 兼容** - 生成的代码完全支持 Native AOT 编译

## 安装

```bash
dotnet add package CyreneJson.BuildTask
```

## 快速开始

### 自动调用点扫描

CyreneJson 会扫描所有调用 `JsonSerializer.Serialize<T>` / `Deserialize<T>` 的地方，自动将泛型参数 `T` 注册为序列化根类型，无需任何手动标注：

```csharp
// 直接调用 —— T 自动被发现，无需额外标注
var json = JsonSerializer.Serialize(myData, options);
var obj  = JsonSerializer.Deserialize<MyData>(json, options);
```

### 包装方法扫描

如果你封装了一层序列化工具类，在方法上标注 `[CyreneEntry]`，调用该方法时传入的泛型类型同样会被自动发现：

```csharp
using CyreneJson.Attributes;

public static class JsonHelper
{
    [CyreneEntry]
    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);

    [CyreneEntry]
    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);
}
```

> 如果载荷不是第一个泛型参数，可以指定索引：`[CyreneEntry(payloadIndex: 1)]`

### 集中注册根类型

对于无法通过调用点静态推断的情况（如通过 `Type` 变量序列化的类型），在一个标记类上使用 `[CyreneEntry(typeof(T))]` 集中声明：

```csharp
using CyreneJson.Attributes;

[CyreneEntry(typeof(GameData))]
[CyreneEntry(typeof(ExcelResource))]
public class CyreneEntries;
```

构建时会自动生成 `CyreneJsonContext`，包含所有注册类型及其属性引用的类型。

### 多态类型

被发现的基类如果存在派生类型，会自动生成多态配置，无需额外标注：

```csharp
public partial class Animal
{
    public string Name { get; set; }
}

public class Cat : Animal { public bool Indoor { get; set; } }
public class Dog : Animal { public string Breed { get; set; } }
```

生成结果：

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Cat), "Cat")]
[JsonDerivedType(typeof(Dog), "Dog")]
partial class Animal { }
```

### 自定义集合处理器

```csharp
using CyreneJson.Attributes;

[CyreneHandler(typeof(MyCollection<>))]
public class MyCollectionHandler;
```

## 注意事项

- **多态基类必须与生成代码位于同一程序集**：对存在派生类型的基类，生成器会发射 `partial class` 来补充 `JsonPolymorphic` / `JsonDerivedType` 配置。`partial` 无法跨程序集扩展，因此参与多态的基类（及其派生类型）必须定义在当前项目源码中，不能来自引用的其他程序集。

- **仅分析当前项目的源码类型**：类型发现、属性扫描、派生类型收集都只针对当前编译中定义的类型。来自 BCL 或其他程序集的类型不会被当作用户类型展开——它们没有源码定义，既无法生成 `partial`，递归展开其成员还会把大量无关的框架类型拖入生成结果。

- **泛型集合作为根类型**：像 `Deserialize<Dictionary<int, UserType>>` 这样以受支持集合为根的调用，集合外壳与其内部的用户类型 `UserType` 都会被正确收集。前提是该集合已通过内置处理器或 `[CyreneHandler]` 注册。

- **基元 / 常见 BCL 根类型被忽略**：`Deserialize<int>`、`Serialize<Guid>` 等以基元或常见 BCL 类型为根的调用会被跳过，不会为其生成注册——`System.Text.Json` 已原生支持这些类型。

## 要求

- .NET 8.0 或更高版本

## 许可证

本项目采用 [MIT](LICENSE.txt)。

## 链接

- [NuGet](https://www.nuget.org/packages/CyreneJson.BuildTask/)
- [GitHub](https://github.com/CyreneLaboratory/CyreneJson)
- [作者](https://github.com/Letheriver2007)
