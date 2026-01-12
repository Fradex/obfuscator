# Obfuscator

## Требования

- .NET SDK 8.0

## Сборка

Запустите скрипт сборки из корня репозитория:

```bash
./build.sh
```

Сборка выполняется в конфигурации `Release` и использует решение `Obfuscator.sln`.

## Запуск

Можно запускать напрямую через `dotnet run`:

```bash
dotnet run --project src/Obfuscator/Obfuscator.csproj -- <input-assembly-path> <output-cs-path>
```

Пример:

```bash
dotnet run --project src/Obfuscator/Obfuscator.csproj -- ./bin/MyApp.dll ./out/obfuscated.cs
```

Либо используйте собранный бинарник после `./build.sh`:

```bash
./src/Obfuscator/bin/Release/net8.0/Obfuscator <input-assembly-path> <output-cs-path>
```
