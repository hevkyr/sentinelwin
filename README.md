# 🛡️ SentinelWin — Agente Local

**Auditoria, privacidade e hardening reversível para Windows 10/11**

🪟 Windows · ⚙️ C# / .NET 8 · 🖥️ WPF · 🔌 Plugin-ready

[![Build](https://github.com/hevkyr/sentinelwin/actions/workflows/build.yml/badge.svg)](https://github.com/hevkyr/sentinelwin/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/status-MVP-orange)]()

---

## 📖 About

Agente desktop do **SentinelWin**: escaneia telemetria, processos suspeitos e serviços de diagnóstico do Windows — e te dá controle granular, reversível e auditável sobre cada alteração.

Sem caixa-preta. Sem promessas mágicas. Toda ação tem diff e rollback.

| | Tweakers tradicionais | **SentinelWin** |
|---|---|---|
| Mostra o que vai mudar | ❌ | ✅ Diff completo antes de aplicar |
| Rollback granular | ❌ | ✅ Snapshot por ação |
| Arquitetura modular | ❌ | ✅ Plugin-ready (`IScanner`) |
| Logs auditáveis | ❌ | ✅ JSON assinado em `%LOCALAPPDATA%` |
| Dry-run mode | ❌ | ✅ Sem alterar nada por padrão |

---

## ✨ Features

- 🔍 Scanner de serviços, registry e hosts file
- 📊 Score de privacidade (0–100) calculado em tempo real
- 💾 Snapshot JSON reversível por ação (`SnapshotStore`)
- 🔌 Sistema de plugins via `AssemblyLoadContext` — adicione scanners sem tocar no core
- 🧪 Dry-run por padrão — nada é alterado sem ação explícita
- 🖥️ UI WPF dark com DataGrid e toolbar
- ⚙️ CI no GitHub Actions (windows-latest)

---

## 📁 Project Structure

```
SentinelWin/
├── SentinelWin.Core/
│   ├── Abstractions/
│   │   ├── IScanner.cs          # Interface: Name + ScanAsync()
│   │   └── IAction.cs           # Interface: ApplyAsync() + RollbackAsync()
│   ├── Models/
│   │   ├── ScanItem.cs          # Record imutável com Id, Type, Risk, Status
│   │   ├── Risk.cs              # Enum: Low | Medium | High
│   │   ├── ActionResult.cs      # Record: Success, Message, SnapshotId
│   │   └── Snapshot.cs          # Record: estado anterior para rollback
│   ├── Scanners/
│   │   ├── ServiceScanner.cs    # Detecta DiagTrack, dmwappushservice, WMPNetworkSvc
│   │   └── RegistryScanner.cs   # Lê AllowTelemetry (HKLM DataCollection)
│   ├── Actions/
│   │   └── ServiceAction.cs     # Para / restaura serviços com rollback
│   └── Services/
│       ├── ScanEngine.cs        # Orquestra scanners, calcula score
│       ├── SnapshotStore.cs     # Persiste JSON em %LOCALAPPDATA%\SentinelWin
│       └── PluginLoader.cs      # Carrega IScanner de DLLs externas
├── SentinelWin.UI/
│   ├── Views/
│   │   ├── MainWindow.xaml      # UI dark com DataGrid
│   │   └── MainWindow.xaml.cs   # Code-behind: ScanBtn + score
│   ├── App.xaml                 # Design tokens (cores, brushes)
│   └── app.manifest             # Requer admin + Windows 10+
├── SentinelWin.Plugins.Sample/
│   └── HostsFileScanner.cs      # Plugin de exemplo: entries no hosts file
├── Directory.Build.props        # LangVersion 12, Nullable enable, versão global
├── global.json                  # SDK 8.0.300+
└── SentinelWin.sln
```

---

## 🚀 Quick Start

**Pré-requisitos:** Windows 10/11 · Visual Studio 2022 (17.8+) · .NET SDK 8.0.300+

```powershell
git clone https://github.com/hevkyr/sentinelwin.git
cd sentinelwin
dotnet restore
dotnet build -c Release
dotnet run --project SentinelWin.UI
```

> A UI requer **execução como Administrador** para aplicar ações reais (registry/serviços).
> Por padrão roda em **Dry-Run** — nenhuma alteração é feita no sistema.

---

## 🔌 Arquitetura de Plugins

Adicionar um novo scanner é implementar uma interface e dropar a DLL:

```csharp
public sealed class MyScanner : IScanner
{
    public string Name => "My Scanner";

    public Task<IReadOnlyList<ScanItem>> ScanAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ScanItem>>(new List<ScanItem>
        {
            new ScanItem(
                Id: "mycheck:001",
                Name: "Algum processo",
                Type: "Process",
                Description: "Faz isso e aquilo.",
                Status: "Running",
                Risk: Risk.Medium,
                Recommendation: "Desativar se não necessário"
            )
        });
    }
}
```

```powershell
# Compile o plugin e coloque na pasta plugins/
dotnet build MyPlugin -c Release -o plugins/
# O PluginLoader descobre automaticamente via AssemblyLoadContext
```

---

## ♻️ Sistema de Rollback

Cada ação gera um `Snapshot` antes de alterar o estado:

```json
{
  "Id": "a3f2b1c4...",
  "CreatedAt": "2026-05-03T14:32:01",
  "ItemId": "svc:DiagTrack",
  "Type": "Service",
  "PreviousState": "DiagTrack",
  "NewState": "Stopped"
}
```

Para reverter: `IAction.RollbackAsync(snapshot)` — o `ServiceAction` lê `PreviousState` e reinicia o serviço original sem string manipulation frágil.

---

## 📊 Privacy Score

```
Score = 100
Penaliza apenas itens ativos/Running (Stopped/Disabled = sem penalidade):
  High   → -15 pts
  Medium → -8 pts
  Low    → -2 pts
```

---

## 🗺️ Roadmap

- [x] **Fase 1** — Scanner de serviços/registry, UI WPF, backup JSON, plugin loader
- [ ] **Fase 2** — Sistema de perfis com diff visual, firewall rules, TaskScanner
- [ ] **Fase 3** — Monitoramento ETW em tempo real, alertas na tray, ProcessScanner
- [ ] **Fase 4** — Detecção comportamental (ML.NET), plugin marketplace assinado, modo enterprise

---

## 🔐 Princípios

1. **Reversível por design** — toda ação tem rollback via snapshot JSON.
2. **Transparente** — diff exato antes de aplicar qualquer mudança.
3. **Seguro contra si mesmo** — não desabilita Windows Update, drivers ou serviços críticos sem confirmação dupla.
4. **Zero telemetria própria** — SentinelWin nunca envia dados para fora.

---

## 🤝 Contribuindo

Pull requests são bem-vindos. Para mudanças grandes, abra uma issue primeiro.

Plugins implementam `IScanner` em `SentinelWin.Core/Abstractions`. Ver `SentinelWin.Plugins.Sample` como referência.

---

## 📜 License

[MIT](LICENSE) © SentinelWin contributors

[🌐 Site & Demo](https://hevkyr.github.io/sentinelwin) · [🇧🇷 Português](#about)
