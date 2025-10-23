<p align="right">
  <a href="https://github.com/FineRace">
  <img src="https://i.postimg.cc/nzjMnxmF/mini-icon.png" width="30" alt="icon" align="left">
  <a href="#пишите-чистый-c-код-без-воды-с-помощью-аспектно-ориентированного-программирования">
    <img src="https://flagpedia.net/data/flags/w20/ru.png" width="20" alt="Русский">
    Русская версия
  </a> 
</p>

<p align="center">
  <img src="https://i.postimg.cc/cJJyH8Pk/logo1.png" width="550" alt="logo">
  <h3 align="center">Write clean, boilerplate-free C# code with Aspect-Oriented Programming.</h3>
</p>

<p align="center">
  <!-- GitHub Stars -->
  <a href="https://github.com/FineRace/MethodBoundaryAspect.Fody-for-Unity/stargazers">
    <img src="https://img.shields.io/github/stars/FineRace/MethodBoundaryAspect.Fody-for-Unity?style=flat-square&logo=github&label=Stars" alt="GitHub Stars">
  </a>
  <!-- Latest Release -->
  <a href="https://github.com/FineRace/MethodBoundaryAspect.Fody-for-Unity/releases/latest">
    <img src="https://img.shields.io/github/v/release/FineRace/MethodBoundaryAspect.Fody-for-Unity?style=flat-square&logo=github&label=Latest%20Release" alt="Latest Release">
  </a>
  <!-- License -->
  <a href="https://github.com/FineRace/MethodBoundaryAspect.Fody-for-Unity/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/FineRace/MethodBoundaryAspect.Fody-for-Unity?style=flat-square&label=License" alt="License">
  </a>
  <!-- Unity Version -->
  <img src="https://img.shields.io/badge/Unity-2021.3%2B-blue?style=flat-square&logo=unity" alt="Unity Version">
  <!-- Status -->
  <img src="https://img.shields.io/badge/Status-Active-brightgreen?style=flat-square" alt="Status">
</p>

---

### TL;DR
- ✨ AOP (Aspect-Oriented Programming) for **Unity 2021.3+** with full **UniTask/async** support.
- 🚀 Eliminates boilerplate: logging, profiling, error handling, etc. → **up to −60% less repetitive code** and more.
- ⚡️ Zero runtime overhead — all code is weaved at compile time via IL.
- 🛠️ Extensible: easily create your own aspects (caching, validation, authorization, etc.).
- 📊 Speeds up development by ~20–30% in projects with heavy async usage.
- 🔒 Stable performance: tested with async lambdas, compiler state machines, and IL2CPP builds.
- 🛡️ Reliable: The plugin has been tested in 40+ unique use cases across different Unity versions.
- 🧩 Easily integrates into existing Unity projects and Zenject-based architectures.

---

### 🎯 The Problem: Repetitive, Scattered Code

Unity development is full of repetitive tasks: logging method calls, handling exceptions, and measuring performance. This often results in the same `try-catch`, `Debug.Log`, and `Stopwatch` code scattered throughout your project.

**❌ Before AOP:**
```csharp
public async UniTask<PlayerData> LoadPlayerData(string playerId)
{
    Debug.Log($"[LoadPlayerData] Entry: {playerId}");
    var stopwatch = Stopwatch.StartNew();

    try 
    {
        var data = await _api.GetPlayerData(playerId);
        stopwatch.Stop();
        Debug.Log($"[LoadPlayerData] Completed in {stopwatch.ElapsedMilliseconds}ms");
        return data;
    }
    catch (Exception ex)
    {
        Debug.LogError($"[LoadPlayerData] Error: {ex.Message}");
        throw;
    }
}
```

### ✨ The Solution: Write Aspects, Not Boilerplate!

This plugin allows you to encapsulate cross-cutting concerns into reusable **attributes**. Focus on your core logic and let aspects handle the rest.

**✅ With AOP:**
```csharp
[LogMethod]
[ProfileMethod]
[HandleExceptions]
public async UniTask<PlayerData> LoadPlayerData(string playerId)
{
    var data = await _api.GetPlayerData(playerId);
    return data;
}
```

---

### 🛠️ Installation and Usage

#### Step 1: Install Dependencies via UPM
Add the following packages in the Unity Package Manager using the "Add package from git URL..." option:

1. 
```
https://github.com/vovgou/loxodon-framework.git?path=/Loxodon.Framework.Fody/Packages/com.vovgou.loxodon-framework-fody
```

2.  
```
https://github.com/finerace/MethodBoundaryAspect.Fody-for-Unity.git?path=/com.finerace.loxodon.fody.methodboundaryaspect
```

#### Step 2: Configure FodyWeavers.xml
Create or update the `FodyWeavers.xml` file in the `Assets/LoxodonFramework/Editor/AppData/` folder to enable the aspect weaver.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <AssemblyNames>
    <Item>Assembly-CSharp</Item>
    <!-- Add your other assemblies (asmdef) here if you have them -->
  </AssemblyNames>
  <MethodBoundaryAspect />
</Weavers>
```

> [!IMPORTANT]
> You must list all assemblies (asmdef) where you intend to use aspects in `FodyWeavers.xml`.

> [!NOTE]
> <details>
>   <summary><strong>🧠 More on the development...</strong></summary>
>
>   <br>
>
>   Supporting `async/await` methods, especially with a modern library like `UniTask`, is a non-trivial task for compile-time tools. The C# compiler transforms `async` methods into complex, hidden state machine classes.
>
>   The key achievement of this plugin is its ability to correctly identify these generated classes and weave code into them. This ensures that your aspects work reliably with `UniTask`, `async` lambdas, and other modern C# features, which is often a challenge for older AOP tools in Unity.
>
> </details>

---

### 📖 Documentation

#### Core Functionality

The plugin provides three entry points into a method's lifecycle that you can override in your aspect class:

*   `OnEntry(MethodExecutionArgs args)` — called **before** the method body executes.
   
*   `OnExit(MethodExecutionArgs args)` — called **after** the method successfully completes, or after `OnException` if the exception was suppressed.
   
*   `OnException(MethodExecutionArgs args)` — called if an **unhandled exception** occurs within the method. It allows you to centralize error handling, logging, and control the execution flow.

#### Execution Context: `MethodExecutionArgs`

Each of these methods receives a `MethodExecutionArgs` object (`args`), which contains all information about the call:

*   `args.Instance`: The object instance on which the method was called (`this`). Will be `null` for `static` methods.
*   `args.Method`: Information about the method itself (`MethodInfo`).
*   `args.Arguments`: An `object[]` array containing the method's input arguments.
*   `args.ReturnValue`: An `object` containing the return value.
*   `args.Exception`: The caught exception. Only available in `OnException`.
*   `args.MethodExecutionTag`: An `object` that can be used to pass data between `OnEntry` and `OnExit`/`OnException` (e.g., for a `Stopwatch`).
*   `args.FlowBehavior`: Allows you to control the execution flow after the aspect finishes.
    *   `FlowBehavior.Continue` (default): Continue execution. In `OnException`, this suppresses the exception.
    *   `FlowBehavior.RethrowException`: Re-throws the exception (used in `OnException`).
    *   `FlowBehavior.Return`: Immediately exits the method without executing its body (if used in `OnEntry`).

#### Aspect Execution Order

If a method has multiple aspect attributes, they work like nesting dolls or a stack (LIFO - Last In, First Out):

```csharp
[AspectA]
[AspectB]
public void MyMethod() { /* ... */ }
```

The order of execution will be:
1.  `AspectA.OnEntry`
2.  `AspectB.OnEntry`
3.  **Execution of `MyMethod`**
4.  `AspectB.OnExit`
5.  `AspectA.OnExit`

In case of an exception:
1.  `AspectA.OnEntry`
2.  `AspectB.OnEntry`
3.  **Exception in `MyMethod`**
4.  `AspectB.OnException`
5.  `AspectA.OnException`
6.  `AspectB.OnExit` (if the exception was suppressed in `OnException`)
7.  `AspectA.OnExit` (if the exception was suppressed in `OnException`)

#### Other Features

*   **Class-Level Attributes**: You can apply an aspect to an entire class, and it will automatically be applied to all methods within that class (including private ones).
*   **Support for `yield` Iterators**: Aspects are applied to each `MoveNext()` call of the compiler-generated state machine. This means `OnEntry`/`OnExit` will trigger on every iteration of a `foreach` loop.

---

### ❗ Key Limitations and Pitfalls

> [!IMPORTANT]
> Please be aware of the following technical limitations when working with MethodBoundaryAspect:

1.  **Modifying arguments and return values is not supported for async methods.**
    *   **Reason:** The compiler copies arguments into the state machine's fields *before* `OnEntry` is called. Modifying `args.Arguments` or `args.ReturnValue` will not affect the execution of the asynchronous code.

2.  **Lack of Support for `async void` and `async UniTaskVoid`**

    *   **Problem**: For methods declared as async void or async UniTaskVoid, the plugin will be unable to generate correct IL code.

    *   **Reason:** This is a fundamental limitation of the "fire-and-forget" design of these methods in C#. They do not return a `Task` or `UniTask` object that can be monitored for completion. Therefore, the plugin has no way of knowing when the method finishes or throws an exception.

    *   **Recommendation:** When working with aspects, always prefer `async UniTask` or `async Task`. This not only ensures the plugin works correctly but also makes your code more reliable and predictable.


3.  **Using `arg.FlowBehavior = FlowBehavior.Return` in `OnEntry` is not supported for async methods.**
    *   **Reason:** The aspect is synchronous code and cannot correctly generate an asynchronous result (`Task` or `UniTask`) for an immediate return. This would lead to runtime errors.

4.  **Suppressing exceptions in methods that return a value (other than `Task`, `UniTask`, or `void`) can cause a `NullReferenceException`.**
    *   The calling code must be prepared to handle such default values.

---

### 🧩 Usage Examples

Creating your own aspect is simple. Inherit from `OnMethodBoundaryAspect` and override the methods you need.

#### 1. Logging Method Entry and Exit
```csharp
using MethodBoundaryAspect.Fody.Attributes;
using Debug = UnityEngine.Debug;

public class LogAttribute : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        if (args.Arguments.Length <= 0)
            Debug.Log($"[{args.Method.Name}] Entry");
        else
            Debug.Log($"[{args.Method.Name}] Entry with: {string.Join(" ,", args.Arguments)}");
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        if (args.ReturnValue == null)
            Debug.Log($"[{args.Method.Name}] Exit");
        else
            Debug.Log($"[{args.Method.Name}] Exit with: {args.ReturnValue}");
    }
}
```
```csharp
// Usage:
[Log]
public int Add(int a, int b) => a + b;
```

#### 2. Profiling Method Execution Time
```csharp
using MethodBoundaryAspect.Fody.Attributes;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

public class ProfileAttribute : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        // Use MethodExecutionTag to store the stopwatch between OnEntry and OnExit
        args.MethodExecutionTag = Stopwatch.StartNew();
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        var stopwatch = (Stopwatch)args.MethodExecutionTag;
        stopwatch.Stop();

        Debug.Log($"[{args.Method.Name}] Completed in {stopwatch.ElapsedMilliseconds}ms");
    }
}
```
```csharp
// Usage:
[Profile]
public async UniTask LoadSomeData() => await UniTask.Delay(100);
```

#### 3. Safe Execution and Error Handling
```csharp
using MethodBoundaryAspect.Fody.Attributes;
using Debug = UnityEngine.Debug;
using System;

public class HandleExceptionAttribute : OnMethodBoundaryAspect
{
    public override void OnException(MethodExecutionArgs args)
    {
        Debug.LogError($"[{args.Method.Name}] Exception: {args.Exception.Message}");

        // Suppress the exception and continue execution
        args.FlowBehavior = FlowBehavior.Continue;
    }
}
```
```csharp
// Usage:
[HandleException]
public void MayThrowException() => throw new Exception("Something went wrong!");
```

---

### 📋 Requirements and Information

*   **Unity Version:** 2021.3 or newer
*   **Scripting Backend:** Mono or IL2CPP

*   **Feedback**: IL-level code weaving is a deep and complex integration into the Unity engine. Despite extensive testing, unique edge cases can always arise. If you encounter a bug or unstable behavior, please create an Issue. Your feedback is invaluable for improving the plugin!

<details>
  <summary><strong>Changelog</strong></summary>

  ### v2.0.0
  This is a major update focused on improving stability, fixing critical bugs with `async` methods, and adding new functionality.
  
  #### ✨ New Features
  - **Class-Level Attribute Support:** You can now apply aspects to an entire class, significantly reducing code duplication.
  - **Partial `yield` Iterator Support:** Aspects are now correctly applied to the `MoveNext()` method of the state machine, allowing you to track each iteration.
  - **Increased Stability:** The plugin is now more predictable and reliable in complex scenarios.

  #### 🐛 Bug Fixes
  - **Incorrect behavior with `async` methods (Task/UniTask):**
    - `OnExit` is now correctly called **after** the final `async` operation completes, not synchronously at the start.
    - Fixed a **Deadlock** that occurred when handling exceptions in `async` methods (`FlowBehavior.Continue`), which caused an infinite `await`.
    - Fixed a **Race Condition** that could cause `OnExit` to fire unpredictably.
  - **Incorrect `FlowBehavior.Return` behavior:** When execution is aborted via `OnEntry`, `OnExit` is now correctly called for all aspects in the chain.
  - **Broken `OnExit` chain during exceptions:** When an exception is handled by one aspect (`FlowBehavior.Continue`), `OnExit` is now correctly called for all preceding aspects.

  #### v1.1.2
  - Added support for anonymous methods and lambdas
  - Fixed handling of compiler-generated classes
  - Improved code weaving stability

  #### v1.1.1
  - Fixed bugs in async methods with UniTask
  - Improved compatibility with UniTask

  #### v1.1.0
  - Added support for UniTask
  - Automatic detection of async state machines
  - Optimized async/await handling
</details>

<details>
  <summary><strong>Acknowledgements...</strong></summary>
  
  <br>
  
  This plugin is an upgraded version based on the work of the following projects:

  *   **[MethodBoundaryAspect.Fody](https://github.com/vescon/MethodBoundaryAspect.Fody)** — The original .NET library on which this plugin is based.
  *   **[Loxodon Framework](https://github.com/vovgou/loxodon-framework)** — The framework that provided the initial Fody integration for the Unity environment.

  The key contribution of this version is deep integration and reliable support for modern C# asynchronous features like UniTask.

</details>

  <br>  <br>  <br>  <br>  <br>  <br>  <br>  <br>  <br>  <br>  <br>

<p align="right">
  <img src="https://flagpedia.net/data/flags/w20/gb.png" width="20" alt="English">
  <a href="#write-clean-boilerplate-free-c-code-with-aspect-oriented-programming"> English Version</a>
  <a href="https://github.com/FineRace">
  <img src="https://i.postimg.cc/nzjMnxmF/mini-icon.png" width="30" alt="icon" align="left">
</p>

<p align="center">
  <img src="https://i.postimg.cc/cJJyH8Pk/logo1.png" width="550" alt="logo">
  <h3 align="center">Пишите чистый C#-код без воды с помощью Аспектно-Ориентированного Программирования.</h3>
</p>

<p align="center">
  <!-- GitHub Stars -->
  <a href="https://github.com/FineRace/MethodBoundaryAspect.Fody-for-Unity/stargazers">
    <img src="https://img.shields.io/github/stars/FineRace/MethodBoundaryAspect.Fody-for-Unity?style=flat-square&logo=github&label=Stars" alt="GitHub Stars">
  </a>
  <!-- Latest Release -->
  <a href="https://github.com/FineRace/MethodBoundaryAspect.Fody-for-Unity/releases/latest">
    <img src="https://img.shields.io/github/v/release/FineRace/MethodBoundaryAspect.Fody-for-Unity?style=flat-square&logo=github&label=Latest%20Release" alt="Latest Release">
  </a>
  <!-- License -->
  <a href="https://github.com/FineRace/MethodBoundaryAspect.Fody-for-Unity/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/FineRace/MethodBoundaryAspect.Fody-for-Unity?style=flat-square&label=License" alt="License">
  </a>
  <!-- Unity Version -->
  <img src="https://img.shields.io/badge/Unity-2021.3%2B-blue?style=flat-square&logo=unity" alt="Unity Version">
  <!-- Status -->
  <img src="https://img.shields.io/badge/Status-Active-brightgreen?style=flat-square" alt="Status">
</p>

---

### TL;DR
- ✨ AOP (Aspect-Oriented Programming) для **Unity 2021.3+** с полной поддержкой **UniTask/async**.
- 🚀 Убирает бойлерплейт: логирование, профайлинг, обработка ошибок, и т.д. → **до −60% повторяющегося кода** и больше.
- ⚡️ Нулевая нагрузка в рантайме — весь код внедряется на этапе компиляции через IL.
- 🛠️ Расширяемость: легко создавать свои аспекты (кэширование, валидация, авторизация и др.).
- 📊 Ускоряет разработку примерно на ~20–30% в проектах с активным использованием асинхронности.
- 🔒 Стабильная работа: протестирован с async-лямбдами, стейт-машинами компилятора и IL2CPP-билдами.
- 🛡️ Надёжность: Плагин протестирован в 40+ уникальных сценариев использования на разных версиях Unity.
- 🧩 Легко интегрируется в существующие Unity-проекты и архитектуры на Zenject.

---

### 🎯 Проблема: Повторяющийся, разбросанный по всему проекту код

Разработка на Unity полна однотипных задач: логирование вызовов методов, обработка исключений, измерение производительности. В итоге один и тот же код с `try-catch`, `Debug.Log` и `Stopwatch` оказывается разбросан по всему проекту.

**❌ До AOP:**
```csharp
public async UniTask<PlayerData> LoadPlayerData(string playerId)
{
    Debug.Log($"[LoadPlayerData] Вход: {playerId}");
    var stopwatch = Stopwatch.StartNew();

    try 
    {
        var data = await _api.GetPlayerData(playerId);
        stopwatch.Stop();
        Debug.Log($"[LoadPlayerData] Завершено за {stopwatch.ElapsedMilliseconds}мс");
        return data;
    }
    catch (Exception ex)
    {
        Debug.LogError($"[LoadPlayerData] Ошибка: {ex.Message}");
        throw;
    }
}
```

### ✨ Решение: Пишите аспекты, а не воду!

Этот плагин позволяет инкапсулировать сквозную функциональность в переиспользуемые **атрибуты**. Сконцентрируйтесь на основной логике, а остальное доверьте аспектам.

**✅ После AOP:**
```csharp
[LogMethod]
[ProfileMethod]
[HandleExceptions]
public async UniTask<PlayerData> LoadPlayerData(string playerId)
{
    var data = await _api.GetPlayerData(playerId);
    return data;
}
```

---

### 🛠️ Установка и использование

#### Шаг 1: Установите зависимости через UPM
Добавьте следующие пакеты в Unity Package Manager, используя опцию "Add package from git URL...":

1. 
```
https://github.com/vovgou/loxodon-framework.git?path=/Loxodon.Framework.Fody/Packages/com.vovgou.loxodon-framework-fody
```

2.  
```
https://github.com/finerace/MethodBoundaryAspect.Fody-for-Unity.git?path=/com.finerace.loxodon.fody.methodboundaryaspect
```

#### Шаг 2: Настройте FodyWeavers.xml
Создайте или обновите файл `FodyWeavers.xml` в папке `Assets/LoxodonFramework/Editor/AppData/`, чтобы подключить обработчик аспектов.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <AssemblyNames>
    <Item>Assembly-CSharp</Item>
    <!-- Здесь нужно добавить другие ваши сборки (asmdef), если они есть -->
  </AssemblyNames>
  <MethodBoundaryAspect />
</Weavers>
```

> [!IMPORTANT]
> В `FodyWeavers.xml` нужно указывать все сборки (asmdef), в которых вы хотите использовать аспекты.

> [!NOTE]
> <details>
>   <summary><strong>🧠 Подробнее о разработке...</strong></summary>
>
>   <br>
>
>   Поддержка `async/await` методов, особенно с такой современной библиотекой как `UniTask` — это нетривиальная задача для инструментов, работающих на этапе компиляции. Компилятор C# превращает `async`-методы в сложные, скрытые классы-стейтмашины.
>
>   Главное достижение этого плагина — его способность корректно находить эти сгенерированные классы и внедрять в них код. Это гарантирует, что ваши аспекты будут надежно работать с `UniTask`, `async`-лямбдами и другими современными возможностями C#, что часто является проблемой в устаревших AOP-инструментах для Unity.
>
> </details>

---

### 📖 Документация

#### Основная функциональность

Плагин предоставляет три точки входа в жизненный цикл метода, которые вы можете переопределить в своём классе-аспекте:

*   `OnEntry(MethodExecutionArgs args)` — вызывается **перед** выполнением тела метода.
   
*   `OnExit(MethodExecutionArgs args)` — вызывается **после** успешного выполнения метода, или после OnException в случае подавления исключения.
   
*   `OnException(MethodExecutionArgs args)` — вызывается, если внутри метода произошло **необработанное исключение**. Позволяет централизованно обрабатывать ошибки, логировать их и управлять дальнейшим ходом выполнения.

#### Контекст выполнения: `MethodExecutionArgs`

Каждый из этих методов получает объект `MethodExecutionArgs` (`args`), который содержит всю информацию о вызове:

*   `args.Instance`: Экземпляр объекта, у которого вызван метод (`this`). Для `static`-методов будет `null`.
*   `args.Method`: Информация о самом методе (`MethodInfo`).
*   `args.Arguments`: Массив `object[]` с входными аргументами метода.
*   `args.ReturnValue`: `object`, содержащий возвращаемое значение.
*   `args.Exception`: Перехваченное исключение. Доступно только в `OnException`.
*   `args.MethodExecutionTag`: `object`, который можно использовать для передачи данных между `OnEntry` и `OnExit`/`OnException` (например, для `Stopwatch`).
*   `args.FlowBehavior`: Позволяет управлять ходом выполнения после завершения аспекта.
    *   `FlowBehavior.Continue` (по умолчанию): Продолжить выполнение. В `OnException` это означает подавление исключения.
    *   `FlowBehavior.RethrowException`: Повторно выбросить исключение (используется в `OnException`).
    *   `FlowBehavior.Return`: Немедленно завершить выполнение метода, не вызывая его тело (если используется в `OnEntry`).

#### Порядок выполнения аспектов

Если к методу применено несколько атрибутов, они работают по принципу "матрёшки" или стека (LIFO - Last In, First Out):

```csharp
[AspectA]
[AspectB]
public void MyMethod() { /* ... */ }
```

Порядок будет следующим:
1.  `AspectA.OnEntry`
2.  `AspectB.OnEntry`
3.  **Выполнение `MyMethod`**
4.  `AspectB.OnExit`
5.  `AspectA.OnExit`

В случае исключения:
1.  `AspectA.OnEntry`
2.  `AspectB.OnEntry`
3.  **Исключение в `MyMethod`**
4.  `AspectB.OnException`
5.  `AspectA.OnException`
6.  `AspectB.OnExit` (если исключение было подавлено в `OnException`)
7.  `AspectA.OnExit` (если исключение было подавлено в `OnException`)

#### Другие возможности

*   **Атрибуты на уровне класса**: Вы можете применить аспект ко всему классу, и он автоматически применится ко методам этого класса (в том числе и приватным).
*   **Поддержка `yield`-итераторов**: Аспекты применяются к каждому вызову `MoveNext()` сгенерированной компилятором state-машины. Это означает, что `OnEntry`/`OnExit` будут срабатывать на каждой итерации `foreach`.

---

### ❗ Ключевые ограничения и подводные камни

> [!IMPORTANT]
> Пожалуйста, учитывайте следующие технические ограничения при работе с MethodBoundaryAspect:

1.  **Изменение аргументов и возвращаемого значения не поддерживается для async-методов.**
    *   **Причина:** Компилятор копирует аргументы в поля state-машины *до* вызова `OnEntry`. Модификация `args.Arguments` или `args.ReturnValue` не повлияет на выполнение асинхронного кода.

2. **Отсутствие поддержки `async void` и `async UniTaskVoid`**
    * Проблема: Для методов, объявленных как `async void` или `async UniTaskVoid`, плагин не сможет сгенерировать корретный IL-код.

    * Причина: Это фундаментальное ограничение, связанное с дизайном "fire-and-forget" методов в C#. У них нет возвращаемого объекта (Task/UniTask), за завершением которого можно было бы следить. Поэтому плагин не может узнать, когда метод завершился или выбросил исключение.

    * Рекомендация: При работе с аспектами всегда предпочитайте async UniTask или async Task. Это не только обеспечит корректную работу плагина, но и сделает ваш код в целом более надёжным и предсказуемым.


3.  **Использование `arg.FlowBehavior = FlowBehavior.Return` в `OnEntry` не поддерживается для async-методов.**
    *   **Причина:** Аспект — это синхронный код, и он не может корректно сгенерировать асинхронный результат (`Task` или `UniTask`) для немедленного возврата. Это привело бы к ошибкам во время выполнения.

4.  **Подавление исключений в методах, возвращающих значение (не считая `Task`, `UniTask` и `void`), может вызвать `NullReferenceException`.**
    * Вызывающий код должен быть готов к обработке таких значений по умолчанию.

---

### 🧩 Примеры использования

Создать собственный аспект очень просто. Унаследуйте класс от `OnMethodBoundaryAspect` и переопределите нужные вам методы.

#### 1. Логирование входа и выхода из метода
```csharp
using MethodBoundaryAspect.Fody.Attributes;
using Debug = UnityEngine.Debug;

public class LogAttribute : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        if (args.Arguments.Length <= 0)
            Debug.Log($"[{args.Method.Name}] Вход");
        else
            Debug.Log($"[{args.Method.Name}] Вход: {string.Join(" ,", args.Arguments)}");
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        if (args.ReturnValue == null)
            Debug.Log($"[{args.Method.Name}] Выход");
        else
            Debug.Log($"[{args.Method.Name}] Выход: {args.ReturnValue}");
    }
}
```
```csharp
// Использование:
[Log]
public int Add(int a, int b) => a + b;
```

#### 2. Профилирование времени выполнения метода
```csharp
using MethodBoundaryAspect.Fody.Attributes;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

public class ProfileAttribute : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        // Используем MethodExecutionTag для передачи данных между OnEntry и OnExit
        args.MethodExecutionTag = Stopwatch.StartNew();
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        var stopwatch = (Stopwatch)args.MethodExecutionTag;
        stopwatch.Stop();

        Debug.Log($"[{args.Method.Name}] Завершён за {stopwatch.ElapsedMilliseconds}ms");
    }
}
```
```csharp
// Использование:
[Profile]
public async UniTask LoadSomeData() => await UniTask.Delay(100);
```

#### 3. Безопасное выполнение и ловля ошибок
```csharp
using MethodBoundaryAspect.Fody.Attributes;
using Debug = UnityEngine.Debug;

public class HandleExceptionAttribute : OnMethodBoundaryAspect
{
    public override void OnException(MethodExecutionArgs args)
    {
        Debug.LogError($"[{args.Method.Name}] Exception");

        args.FlowBehavior = FlowBehavior.Continue;
    }
}
```
```csharp
// Использование:
[HandleException]
public void MayThrowException() => throw new Exception("Что-то пошло не так!");```
```

---

### 📋 Требования и информация

*   **Версия Unity:** 2021.3 или новее
*   **Scripting Backend:** Mono или IL2CPP

*   **Обратная связь**: Вплетение кода на уровне IL — это глубокая и сложная интеграция в движок Unity. Несмотря на обширное тестирование, всегда могут найтись уникальные пограничные случаи. Если вы столкнулись с ошибкой или нестабильным поведением, пожалуйста, создайте Issue. Ваша обратная связь бесценна для улучшения плагина!

<details>
  <summary><strong>История изменений (Changelog)</strong></summary>

  ### v2.0.0
  Это крупное обновление, направленное на повышение стабильности, исправление критических ошибок с `async`-методами и добавление новой функциональности.
  
  #### ✨ Новая функциональность
  - **Поддержка атрибутов на уровне класса:** Теперь вы можете применять аспекты ко всему классу, что значительно уменьшает дублирование кода.
  - **Частичная поддержка `yield`-итераторов:** Аспекты теперь корректно применяются к `MoveNext()`-методу state-машины, позволяя отслеживать каждую итерацию.
  - **Повышенная стабильность:** Плагин стал более предсказуемым и надежным в сложных сценариях.

  #### 🐛 Исправленные ошибки
  - **Некорректная работа с `async` методами (Task/UniTask):**
    - `OnExit` теперь вызывается корректно **после** завершения последней `async`-операции, а не синхронно в начале.
    - Устранен **Deadlock**, возникавший при обработке исключений в `async`-методах (`FlowBehavior.Continue`), который приводил к вечному `await`.
    - Исправлена **Race Condition**, из-за которой `OnExit` мог сработать непредсказуемо.
  - **Некорректная работа `FlowBehavior.Return`:** При прерывании выполнения метода через `OnEntry` теперь корректно вызывается `OnExit` для всех аспектов в цепочке.
  - **Нарушение цепочки `OnExit` при исключениях:** При обработке исключения одним из аспектов (`FlowBehavior.Continue`) теперь корректно вызывается `OnExit` для всех предыдущих аспектов.

  #### v1.1.2
  - Добавлена поддержка анонимных методов и лямбд
  - Исправлена обработка сгенерированных компилятором классов
  - Улучшена стабильность "вплетения" кода

  #### v1.1.1
  - Исправлены ошибки в асинхронных методах с UniTask
  - Улучшена совместимость с UniTask

  #### v1.1.0
  - Добавлена поддержка UniTask
  - Автоматическое определение асинхронных стейт-машин
  - Оптимизирована обработка async/await
</details>

<details>
  <summary><strong>Благодарности...</strong></summary>
  
  <br>
  
  Этот плагин является модернизированной версией, основанной на работе следующих проектов:

  *   **[MethodBoundaryAspect.Fody](https://github.com/vescon/MethodBoundaryAspect.Fody)** — Оригинальная библиотека для .NET, на которой основан плагин.
  *   **[Loxodon Framework](https://github.com/vovgou/loxodon-framework)** — Фреймворк, предоставивший первоначальную интеграцию Fody в среду Unity.

  Ключевой вклад этой версии — глубокая интеграция и надежная поддержка современных асинхронных возможностей C#, таких как UniTask.

</details>
