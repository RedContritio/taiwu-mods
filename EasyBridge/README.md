# EasyBridge

自动化测试桥（开发/调试工具，非创意工坊发布目标）。**一个 mod、两个插件、两条命名管道**，供 LL/自动化 agent
端到端验证其它 mod（如 ForceEncounter）的各分支路径：

| 插件 | 进程 | 命名管道 | 作用 | 技能 |
|------|------|---------|------|------|
| `EasyBridge.Frontend` | 前端 Unity（Mono/net48） | `taiwu-uibridge` | 语义化检视/操控游戏 **UI**（click/toggle/set/select、等待窗口） | `skills/taiwu-ui.md` |
| `EasyBridge.Backend` | 后端 GameData（.NET 8） | `taiwu-testbridge` | 读取/构造**角色与关系状态**（生成符合条件的 NPC）、瞬移、伤势、捕绳，以及 **`/eval` 动态执行任意 C#** | `skills/taiwu-statebridge.md` |

> 前后端是两个进程：UI 在 Unity 前端，角色数据在 .NET 8 后端，一个插件 DLL 只能跑在一个进程里，所以需要两个插件；
> 这里把它们打包成**同一个 mod「EasyBridge」**（参照 ExampleMod 的双插件模式），游戏里只显示一个条目。

协议：每条管道单行 JSON 请求 → 单行 JSON 响应。连接函数见对应技能文件。

## 后端状态桥端点（`taiwu-testbridge`）

读：`/ping`（含 `tickAlive`）、`/taiwu`、`/whereami`、`/char/{id}`、`/block/chars`、`/sects`、`/settlements`。
构造/改写（均在后端主线程、用主线程写上下文执行）：`/spawn`、`/spawn/closefriend`、`/preset`、`/relation`、`/favor`、
`/favor/exact`、`/villager`、`/nonvillager`、`/prisoner`、`/injure`、`/heal`、`/cripple`、`/giverope`、`/throwrope`、
`/move/taiwu`。详细参数与预设见 `skills/taiwu-statebridge.md`。

### `/eval`：动态执行任意 C#

后端内置 Roslyn（CSharpScript）。`/eval {code}` 把 C# 在后端主线程上、用主线程写上下文 `ctx` 运行，脚本里已 `using`
`DomainManager.*` / `EventHelper.*` / `GameOps.*`，预置全局 `ctx`(DataContext) 与 `taiwuId`(int)，用 `return ...;` 返回，
结果序列化进 `result`（Dictionary/基元/字符串/可枚举 → JSON，其它 → ToString），编译/运行错误返回 `{ok:false,error}`。
用它做一次性/临时操作，**不必再为每个新操作加端点重编译重启**；常用流程仍走上面的命名端点。

```powershell
SB -Path "/eval" -Obj @{ code = "return DomainManager.Taiwu.GetTaiwuCharId();" }            # → 6818
SB -Path "/eval" -Obj @{ code = "DomainManager.Character.AddRelation(ctx, taiwuId, 7570, 1024); return DomainManager.Character.GetAliveSpouse(7570);" }
SB -Path "/eval" -Obj @{ code = "return GameOps.Taiwu();" }                                  # → 完整快照字典
```

## 构建 / 部署

```powershell
# 构建（前端 net48 + 后端 net8，输出到 EasyBridge/Plugins）
dotnet build EasyBridge/EasyBridge.Backend/EasyBridge.Backend.csproj -c Release
dotnet build EasyBridge/EasyBridge.Frontend/EasyBridge.Frontend.csproj -c Release
# 部署到游戏（同时带上后端捆绑的 Roslyn DLL）
pwsh ./deploy.ps1 -ModName EasyBridge -IncludeDrafts
```

要点：
- 后端 `EasyBridge.Backend.csproj` 用 `CopyLocalLockFileAssemblies=true` 把 4 个 `Microsoft.CodeAnalysis*.dll` 拷进 `Plugins`
  （net8 的 `System.*` 在框架内、不拷）；`SatelliteResourceLanguages=en` 去掉本地化卫星目录。
- `Copy-ModFiles` 会把 `Plugins` 里非声明插件的依赖 DLL（即 Roslyn）一并带到部署目录。
- 游戏后端插件加载器把插件及其直接引用按字节加载、且其依赖解析是死代码，所以 `BackendPlugin` 自己挂了
  `AssemblyLoadContext.Default.Resolving`，用 `LoadFromAssemblyPath` 从本 mod 的 Plugins 目录解析 Roslyn 的传递依赖；
  脚本对本 mod 程序集的引用用 `CreateFromImage`（仅编译期 metadata）+ 钩子返回已加载的那份，避免跨 ALC 的类型身份冲突。
- **本地 mod 需在「模组管理」里启用并重启游戏才加载**；后端 DLL 改动也必须重启游戏。

## 验证用途

配合两条管道可端到端验证 ForceEncounter 全部可达分支（亲密直通各源、强制战斗各结果、护卫拦截、战斗擒获→处置、
无力应战、未成年/村民文案等）。具体配方见两个技能文件。
