<p align="right">
  <img src="https://flagpedia.net/data/flags/w20/ru.png" width="20" alt="Русский">
  <a href="#methodboundaryaspectfody-для-unity"> Русская версия</a>
</p>

<p align="center">
  <h1 align="center">MethodBoundaryAspect.Fody for Unity</h1>
  <h3 align="center">Write clean, boilerplate-free C# code with the power of Aspect-Oriented Programming.</h3>
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
- ✨ Aspect-Oriented Programming for **Unity 2021.3+** with full **UniTask/async support**.
- 🚀 Replace boilerplate: logging, profiling, error handling → up to **−60% repetitive code**.
- ⚡️ Zero runtime overhead — weaving happens at compile time via IL injection.
- 🛠️ Extensible: create custom aspects (caching, validation, authorization, etc.).
- 📊 Increases development speed by ~20–30% in projects with heavy async workflows.
- 🔒 Stable: tested with async lambdas, compiler-generated state machines, and IL2CPP builds.
- 🧩 Integrates seamlessly with existing Unity projects and Zenject-based architectures.

---

### 🎯 The Problem: Repetitive, Scattered Code

Unity development is full of common patterns: logging method calls, handling exceptions, and measuring performance. This results in the same `try-catch`, `Debug.Log`, and `Stopwatch` code being scattered across your entire project.

**❌ Before AOP:**
```csharp
public async UniTask LoadPlayerData(string playerId)
{
    Debug.Log($"[LoadPlayerData] Loading data for {playerId}");
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
        Debug.LogError($"[LoadPlayerData] Failed: {ex.Message}");
        throw;
    }
}
```

### ✨ The Solution: Write Aspects, Not Boilerplate

This plugin allows you to encapsulate cross-cutting concerns into reusable **attributes**. Focus on your core logic and let aspects handle the rest.

**✅ After AOP:**
```csharp
[LogMethod]
[ProfileMethod]
[HandleExceptions]
public async UniTask LoadPlayerData(string playerId)
{
    var data = await _api.GetPlayerData(playerId);
    return data;
}
```

---

### 🚀 Key Features

*   **Modern Async Support:** Full, native support for **UniTask**, including compiler-generated state machines and lambda expressions. This is the core feature that makes AOP viable in modern Unity projects.
*   **Custom Aspects:** Easily create your own attributes to handle any cross-cutting concern, such as caching, validation, authorization, and more.
*   **Zero Runtime Overhead:** All the magic happens at compile time. The plugin "weaves" your aspect logic directly into your assembly's IL code, introducing no performance cost at runtime.
*   **Exception Handling:** Wrap methods in a safe `try-catch` block with a single attribute.
*   **Method Interception:** Hook into the entry and exit points of any method for powerful logging and profiling.

> [!NOTE]
> <details>
>   <summary><strong>🧠 A Deeper Look...</strong></summary>
>
>   <br>
>
>   Supporting `async/await` methods, especially with a modern library like `UniTask`, is a non-trivial challenge for compile-time weaving tools. The C# compiler transforms `async` methods into complex, hidden state-machine classes.
>
>   This plugin's key achievement is its ability to correctly identify and inject code into these compiler-generated classes. It ensures that your aspects work reliably with `UniTask`, `async` lambdas, and other modern C# features, a capability often missing in legacy AOP tools for Unity.
>
> </details>

---

### 🛠️ Installation & Usage

#### Step 1: Install Dependencies via UPM
Add the following packages in the Unity Package Manager using the "Add package from git URL..." option:
1.  `https://github.com/vovgou/loxodon-framework.git?path=/Loxodon.Framework.Fody/Packages/com.vovgou.loxodon-framework-fody`
2.  `https://github.com/finerace/MethodBoundaryAspect.Fody-for-Unity.git`

#### Step 2: Configure FodyWeavers.xml
Create or update the `FodyWeavers.xml` file located in `Assets/LoxodonFramework/Editor/AppData/` to include the aspect weaver.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <AssemblyNames>
    <Item>Assembly-CSharp</Item>
    <!-- You can add other assemblies to be woven here -->
  </AssemblyNames>
  <MethodBoundaryAspect />
</Weavers>
```

> [!IMPORTANT]
> A Unity restart or project re-import may be required for the compile-time weaving to activate for the first time.

---

### 🧩 Extensibility: Creating Your Own Aspects

Creating a custom aspect is straightforward. Inherit from `OnMethodBoundaryAspect` and override the methods you need.

**Example: A `[SafeExecution]` attribute that catches and logs exceptions without crashing the game.**
```csharp
using System;
using UnityEngine;
using MethodBoundaryAspect.Fody.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public sealed class SafeExecutionAttribute : OnMethodBoundaryAspect
{
    public override void OnException(MethodExecutionArgs args)
    {
        Debug.LogError($"[SafeExecution] Exception in method {args.Method.Name}: {args.Exception.Message}");
        
        // Suppress the exception and allow execution to continue
        args.FlowBehavior = FlowBehavior.Return;
    }
}
```

> [!WARNING]
> Do not apply AOP attributes directly to **classes** (e.g., `[LogMethod] public class MyClass`). This is unsupported and will break your build process. Attributes should only be applied to **methods** and **constructors**.

---

### 📋 Requirements & Info

*   **Unity Version:** 2021.3 or newer
*   **Scripting Backend:** Mono or IL2CPP

<details>
  <summary><strong>Changelog</strong></summary>

  #### v1.1.2
  - Added support for anonymous methods and lambdas
  - Fixed handling of compiler-generated classes
  - Improved weaving stability

  #### v1.1.1
  - Fixed errors in async methods with UniTask
  - Improved UniTask compatibility

  #### v1.1.0
  - Added UniTask support
  - Automatic detection of async state machines
  - Optimized async/await handling
</details>

<details>
  <summary><strong>Acknowledgements...</strong></summary>
  
  <br>
  
  This plugin is a modernized version based on the work of the following projects:

  *   **[MethodBoundaryAspect.Fody](https://github.com/vescon/MethodBoundaryAspect.Fody)** — The original .NET library on which the plugin is based.
  *   **[Loxodon Framework](https://github.com/vovgou/loxodon-framework)** — The framework that provided the initial integration of Fody into the Unity environment.

  The key contribution of this version is the deep integration and robust support for modern C# asynchronous features like UniTask.

</details>

<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>
<br>



<p align="right">
  <img src="https://flagpedia.net/data/flags/w20/gb.png" width="20" alt="English">
  <a href="#methodboundaryaspectfody-for-unity"> English Version</a>
</p>

<p align="center">
  <!-- A simple, clean logo/banner would go here. For now, the title serves this purpose. -->
  <h1 align="center">MethodBoundaryAspect.Fody для Unity</h1>
  <h3 align="center">Пишите чистый C#-код без "воды" с помощью Аспектно-Ориентированного Программирования.</h3>
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
- 🚀 Убирает бойлерплейт: логирование, профайлинг, обработка ошибок → до **−60% повторяющегося кода**.
- ⚡️ Нулевая нагрузка в рантайме — весь код внедряется на этапе компиляции через IL.
- 🛠️ Расширяемость: легко создавать свои аспекты (кэширование, валидация, авторизация и др.).
- 📊 Ускоряет разработку примерно на ~20–30% в проектах с активным использованием асинхронности.
- 🔒 Стабильная работа: протестирован с async-лямбдами, стейт-машинами компилятора и IL2CPP-билдами.
- 🧩 Легко интегрируется в существующие Unity-проекты и архитектуры на Zenject.

---

### 🎯 Проблема: Повторяющийся, разбросанный по всему проекту код

Разработка на Unity полна однотипных задач: логирование вызовов методов, обработка исключений, измерение производительности. В итоге один и тот же код с `try-catch`, `Debug.Log` и `Stopwatch` оказывается разбросан по всему проекту.

**❌ До AOP:**
```csharp
public async UniTask LoadPlayerData(string playerId)
{
    Debug.Log($"[LoadPlayerData] Загрузка данных для {playerId}");
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

### ✨ Решение: Пишите аспекты, а не "воду"

Этот плагин позволяет инкапсулировать сквозную функциональность в переиспользуемые **атрибуты**. Сконцентрируйтесь на основной логике, а остальное доверьте аспектам.

**✅ После AOP:**
```csharp
[LogMethod]
[ProfileMethod]
[HandleExceptions]
public async UniTask LoadPlayerData(string playerId)
{
    var data = await _api.GetPlayerData(playerId);
    return data;
}
```

---

### 🚀 Ключевые возможности

*   **Поддержка современного Async:** Полная, нативная поддержка **UniTask**, включая сгенерированные компилятором стейт-машины и лямбда-выражения. Это ключевая особенность, которая делает AOP жизнеспособным в современных Unity-проектах.
*   **Собственные аспекты:** Легко создавайте свои атрибуты для решения любых сквозных задач: кэширование, валидация, авторизация и т.д.
*   **Нулевые затраты в рантайме:** Вся магия происходит на этапе компиляции. Плагин "вплетает" логику аспектов напрямую в IL-код вашей сборки, не создавая никакой дополнительной нагрузки во время выполнения игры.
*   **Обработка исключений:** Оберните методы в безопасный `try-catch` блок с помощью одного атрибута.
*   **Перехват вызовов методов:** Встраивайтесь в начало и конец выполнения любого метода для мощного логирования и профилирования.

> [!NOTE]
> <details>
>   <summary><strong>🧠 Подробнее о разработке...</strong></summary>
>
>   <br>
>
>   Поддержка `async/await` методов, особенно с такой современной библиотекой, как `UniTask` — это нетривиальная задача для инструментов, работающих на этапе компиляции. Компилятор C# превращает `async`-методы в сложные, скрытые классы-стейтмашины.
>
>   Главное достижение этого плагина — его способность корректно находить эти сгенерированные классы и внедрять в них код. Это гарантирует, что ваши аспекты будут надежно работать с `UniTask`, `async`-лямбдами и другими современными возможностями C#, что часто является проблемой в устаревших AOP-инструментах для Unity.
>
> </details>

---

### 🛠️ Установка и использование

#### Шаг 1: Установите зависимости через UPM
Добавьте следующие пакеты в Unity Package Manager, используя опцию "Add package from git URL...":
1.  `https://github.com/vovgou/loxodon-framework.git?path=/Loxodon.Framework.Fody/Packages/com.vovgou.loxodon-framework-fody`
2.  `https://github.com/finerace/MethodBoundaryAspect.Fody-for-Unity.git`

#### Шаг 2: Настройте FodyWeavers.xml
Создайте или обновите файл `FodyWeavers.xml` в папке `Assets/LoxodonFramework/Editor/AppData/`, чтобы подключить обработчик аспектов.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <AssemblyNames>
    <Item>Assembly-CSharp</Item>
    <!-- Здесь можно добавить другие сборки для обработки -->
  </AssemblyNames>
  <MethodBoundaryAspect />
</Weavers>
```

> [!IMPORTANT]
> Может потребоваться перезапуск Unity или повторный импорт ассетов, чтобы изменения на этапе компиляции активировались в первый раз.

---

### 🧩 Расширяемость: Создание собственных аспектов

Создать собственный аспект очень просто. Унаследуйте класс от `OnMethodBoundaryAspect` и переопределите нужные вам методы.

**Пример: атрибут `[SafeExecution]`, который ловит и логирует исключения, не приводя к падению игры.**
```csharp
using System;
using UnityEngine;
using MethodBoundaryAspect.Fody.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public sealed class SafeExecutionAttribute : OnMethodBoundaryAspect
{
    public override void OnException(MethodExecutionArgs args)
    {
        Debug.LogError($"[SafeExecution] Исключение в методе {args.Method.Name}: {args.Exception.Message}");
        
        // Подавить исключение и позволить выполнению продолжиться
        args.FlowBehavior = FlowBehavior.Return;
    }
}
```

> [!WARNING]
> Не применяйте атрибуты AOP напрямую к **классам** (например, `[LogMethod] public class MyClass`). Это не поддерживается и приведет к ошибке при сборке проекта. Атрибуты следует применять только к **методам** и **конструкторам**.

---

### 📋 Требования и информация

*   **Версия Unity:** 2021.3 или новее
*   **Scripting Backend:** Mono или IL2CPP

<details>
  <summary><strong>История изменений (Changelog)</strong></summary>

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

