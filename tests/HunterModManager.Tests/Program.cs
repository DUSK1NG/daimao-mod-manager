using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using HunterModManager.Core;

var sevenZip = Environment.GetEnvironmentVariable("HMM_7ZIP") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
var rarFixture = Environment.GetEnvironmentVariable("HMM_RAR_FIXTURE");
var multivariantFixture = Environment.GetEnvironmentVariable("HMM_MULTIVARIANT_FIXTURE");
if ((args.Length == 3 || args.Length == 4) && args[0] == "--inspect" && Enum.TryParse<GameId>(args[1], true, out var inspectionGame))
{
    var inspected = await new ArchiveService(sevenZip).AnalyzeAsync(args[2], inspectionGame);
    Console.WriteLine($"{Games.Display(inspectionGame)}: {Path.GetFileName(inspected.ArchivePath)}");
    if (args.Length == 4) Console.WriteLine("游戏目录：" + (Games.ValidateRoot(inspectionGame, args[3]) ?? "已验证"));
    foreach (var note in inspected.Notes) Console.WriteLine("提示: " + note);
    foreach (var variant in inspected.Variants)
    {
        Console.WriteLine($"版本 {variant.Name}: {variant.Files.Count} 文件; {(variant.BlockedReason ?? "可导入")}");
        foreach (var file in variant.Files.Take(5))
            Console.WriteLine("  " + file.Target + (args.Length == 4 ? (File.Exists(Path.Combine(args[3], file.Target.Replace('/', Path.DirectorySeparatorChar))) ? " [已有文件，将备份]" : " [新文件]") : ""));
    }
    return;
}
var root = Path.Combine(Path.GetTempPath(), "HunterModManager.IntegrationTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
var failed = new List<string>();

try
{
    if (!File.Exists(sevenZip)) throw new FileNotFoundException("测试要求本机安装 7-Zip", sevenZip);
    var archiveService = new ArchiveService(sevenZip);

    await Run("ZIP 与 7Z 分析、导入及启停", async () =>
    {
        foreach (var extension in new[] { ".zip", ".7z" })
        {
            var archive = await CreateModArchiveAsync(root, extension, "version-one");
            var analysis = await archiveService.AnalyzeAsync(archive, GameId.World);
            Check(analysis.Variants.Count == 1, $"{extension}: 应识别一个版本");
            Check(analysis.Variants[0].Files.Single().Target == "nativePC/plugins/mod.txt", $"{extension}: 目标路径应保留 nativePC 锚点");
            Check(analysis.Sha256.Length == 64, $"{extension}: 应计算 SHA-256");
            var sandbox = Path.Combine(root, "deploy-" + extension.TrimStart('.'));
            var game = CreateGame(sandbox, GameId.World);
            var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
            var mod = await manager.ImportAsync(archive, GameId.World, game);
            await manager.SetEnabledAsync(mod.Id, true);
            var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
            Check(File.ReadAllText(target) == "version-one", $"{extension}: 启用应部署包内文件");
            await manager.SetEnabledAsync(mod.Id, false);
            Check(!File.Exists(target), $"{extension}: 停用应撤回新增文件");
        }
    });

    await Run("多版本 ZIP 仅导入所选版本", async () =>
    {
        var sandbox = Path.Combine(root, "variants");
        var game = CreateGame(sandbox, GameId.World);
        var archive = await CreateZipAsync(Path.Combine(sandbox, "variants.zip"),
            ("Option A/nativePC/plugins/mod.txt", "A"),
            ("Option B/nativePC/plugins/mod.txt", "B"));
        var analysis = await archiveService.AnalyzeAsync(archive, GameId.World);
        Check(analysis.Variants.Select(x => x.Name).Order().SequenceEqual(new[] { "Option A", "Option B" }), "应识别两个独立安装版本");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var selected = await manager.ImportAsync(archive, GameId.World, game, "Option B");
        Check(selected.Variant == "Option B" && selected.Files.Count == 1, "记录中只能包含选定版本的文件");
        Check(selected.Files[0].Source.StartsWith("Option B/", StringComparison.Ordinal), "文件来源必须属于所选版本");
        await manager.SetEnabledAsync(selected.Id, true);
        Check(File.ReadAllText(Path.Combine(game, "nativePC", "plugins", "mod.txt")) == "B", "部署内容应来自所选版本");
    });

    await Run("未选择多版本包选项时返回校验错误", async () =>
    {
        var sandbox = Path.Combine(root, "variant-required");
        var game = CreateGame(sandbox, GameId.World);
        var archive = await CreateZipAsync(Path.Combine(sandbox, "variants.zip"),
            ("Option A/nativePC/plugins/mod.txt", "A"),
            ("Option B/nativePC/plugins/mod.txt", "B"));
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        await Throws<InvalidDataException>(() => manager.ImportAsync(archive, GameId.World, game), "未指定多版本选项时应给出可处理的校验错误");
    });

    await Run("无锚点包手动映射", async () =>
    {
        var sandbox = Path.Combine(root, "manual-map");
        var game = CreateGame(sandbox, GameId.World);
        var archive = await CreateZipAsync(Path.Combine(sandbox, "loose.zip"), ("bundle/payload.bin", "mapped"));
        var unmapped = await archiveService.AnalyzeAsync(archive, GameId.World);
        Check(unmapped.Variants.Count == 0, "无锚点包不应自动猜测目标目录");
        var mapped = await archiveService.AnalyzeAsync(archive, GameId.World, "nativePC/plugins");
        Check(mapped.Variants.Single().Files.Single().Target == "nativePC/plugins/bundle/payload.bin", "手动映射应保留包内相对路径");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var mod = await manager.ImportAsync(archive, GameId.World, game, manualTarget: "nativePC/plugins");
        await manager.SetEnabledAsync(mod.Id, true);
        Check(File.ReadAllText(Path.Combine(game, "nativePC", "plugins", "bundle", "payload.bin")) == "mapped", "手动映射文件应部署到模拟目录");
    });

    await Run("PAK 和安装器混合包禁止部分安装", async () =>
    {
        var sandbox = Path.Combine(root, "mixed-packages");
        var game = CreateGame(sandbox, GameId.World);
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var pakArchive = await CreateZipAsync(Path.Combine(sandbox, "mixed-pak.zip"),
            ("nativePC/plugins/mod.txt", "mod"), ("re_chunk_000.pak", "pak"));
        var pakAnalysis = await archiveService.AnalyzeAsync(pakArchive, GameId.World);
        Check(pakAnalysis.Variants.Single().BlockedReason?.Contains("PAK", StringComparison.OrdinalIgnoreCase) == true, "含 PAK 的混合包应标记为阻止安装");
        await Throws<InvalidDataException>(() => manager.ImportAsync(pakArchive, GameId.World, game), "含 PAK 的包不应允许只安装散文件");

        var installerArchive = await CreateZipAsync(Path.Combine(sandbox, "mixed-installer.zip"),
            ("nativePC/plugins/mod.txt", "mod"), ("setup.exe", "installer"));
        var installerAnalysis = await archiveService.AnalyzeAsync(installerArchive, GameId.World);
        Check(installerAnalysis.Variants.Single().BlockedReason?.Contains("安装程序", StringComparison.Ordinal) == true, "含独立安装器的混合包应标记为阻止安装");
        await Throws<InvalidDataException>(() => manager.ImportAsync(installerArchive, GameId.World, game), "含安装器的包不应允许只安装散文件");
        Check(!File.Exists(Path.Combine(game, "nativePC", "plugins", "mod.txt")), "被阻止的混合包不能写入游戏模拟目录");
        Check(manager.Mods.Count == 0, "被阻止的混合包不能进入已导入列表");
    });

    await Run("Stracker 前置配方仅写入模拟目录", async () =>
    {
        var sandbox = Path.Combine(root, "stracker-prerequisite");
        var game = CreateGame(sandbox, GameId.World);
        var archive = await CreateZipAsync(Path.Combine(sandbox, "stracker.zip"),
            ("dinput8.dll", "loader-entry"), ("loader.dll", "stracker-loader"),
            ("loader-config.json", "{}"), ("QuestLoader.dll", "quest-loader"));
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var prerequisite = await manager.ImportPrerequisiteAsync(archive, PrerequisiteKind.StrackersLoader, GameId.World, game);
        Check(prerequisite.IsPrerequisite && prerequisite.Prerequisite == PrerequisiteKind.StrackersLoader, "应将压缩包作为 Stracker 前置导入");
        Check(prerequisite.Files.Select(x => x.Target).Order().SequenceEqual(new[] { "dinput8.dll", "loader-config.json", "loader.dll", "nativePC/plugins/QuestLoader.dll" }.Order()), "配方应选取并映射 Stracker 所需文件");
        Check(Path.GetFullPath(prerequisite.GameRoot).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase), "前置目标必须是测试临时模拟目录");
        File.WriteAllText(Path.Combine(game, "dinput8.dll"), "preexisting-loader");
        await manager.SetEnabledAsync(prerequisite.Id, true);
        Check(File.ReadAllText(Path.Combine(game, "dinput8.dll")) == "loader-entry", "应在模拟目录部署所选 dinput8.dll");
        Check(File.ReadAllText(Path.Combine(game, "loader.dll")) == "stracker-loader", "应在模拟目录部署 loader.dll");
        Check(File.ReadAllText(Path.Combine(game, "nativePC", "plugins", "QuestLoader.dll")) == "quest-loader", "可选 QuestLoader 应映射到 nativePC/plugins");
        await manager.SetEnabledAsync(prerequisite.Id, false);
        Check(File.ReadAllText(Path.Combine(game, "dinput8.dll")) == "preexisting-loader", "停用前置应恢复模拟目录中的既有文件");
        Check(!File.Exists(Path.Combine(game, "loader.dll")), "停用前置应移除模拟目录中新增的文件");
    });

    await Run("崛起脚本与散文件前置门槛", async () =>
    {
        var sandbox = Path.Combine(root, "rise-prerequisites");
        var game = CreateGame(sandbox, GameId.Rise);
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var script = await CreateZipAsync(Path.Combine(sandbox, "script.zip"), ("reframework/autorun/example.lua", "return true"));
        var scriptMod = await manager.ImportAsync(script, GameId.Rise, game);
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(scriptMod.Id, true), "缺少 REFramework 时不能启用脚本");
        var framework = await CreateZipAsync(Path.Combine(sandbox, "reframework.zip"), ("dinput8.dll", "fixture"), ("README.txt", "not deployed"));
        var frameworkMod = await manager.ImportPrerequisiteAsync(framework, PrerequisiteKind.REFramework, GameId.Rise, game);
        await manager.SetEnabledAsync(frameworkMod.Id, true);
        await manager.SetEnabledAsync(scriptMod.Id, true);
        Check(File.Exists(Path.Combine(game, "reframework", "autorun", "example.lua")), "脚本应部署到 REFramework autorun");
        var natives = await CreateZipAsync(Path.Combine(sandbox, "natives.zip"), ("natives/STM/example.bin", "fixture"));
        var nativesMod = await manager.ImportAsync(natives, GameId.Rise, game);
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(nativesMod.Id, true), "缺少 FirstNatives 时不能启用散文件");
        var firstNatives = await CreateZipAsync(Path.Combine(sandbox, "firstnatives.zip"), ("FirstNatives.dll", "fixture"));
        var firstNativesMod = await manager.ImportPrerequisiteAsync(firstNatives, PrerequisiteKind.FirstNatives, GameId.Rise, game);
        await manager.SetEnabledAsync(firstNativesMod.Id, true);
        await manager.SetEnabledAsync(nativesMod.Id, true);
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(firstNativesMod.Id, false), "仍有散文件使用时不能停用 FirstNatives");
    });

    await Run("荒野散文件需 Loose File Loader 设置", async () =>
    {
        var sandbox = Path.Combine(root, "wilds-prerequisites");
        var game = CreateGame(sandbox, GameId.Wilds);
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var framework = await CreateZipAsync(Path.Combine(sandbox, "reframework.zip"), ("dinput8.dll", "fixture"));
        var frameworkMod = await manager.ImportPrerequisiteAsync(framework, PrerequisiteKind.REFramework, GameId.Wilds, game);
        await manager.SetEnabledAsync(frameworkMod.Id, true);
        var natives = await CreateZipAsync(Path.Combine(sandbox, "natives.zip"), ("natives/STM/example.bin", "fixture"));
        var nativesMod = await manager.ImportAsync(natives, GameId.Wilds, game);
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(nativesMod.Id, true), "设置缺失时不能启用散文件");
        await File.WriteAllTextAsync(Path.Combine(game, "re2_fw_config.txt"), "LooseFileLoader_Enabled = true\n");
        await manager.SetEnabledAsync(nativesMod.Id, true);
        Check(File.Exists(Path.Combine(game, "natives", "STM", "example.bin")), "设置存在时应部署散文件");
    });

    await Run("Steam 版拒绝其他平台 natives 路径", async () =>
    {
        var archive = await CreateZipAsync(Path.Combine(root, "console-natives.zip"), ("natives/NSW/example.bin", "fixture"));
        foreach (var game in new[] { GameId.Rise, GameId.Wilds })
        {
            var analysis = await archiveService.AnalyzeAsync(archive, game);
            Check(analysis.Variants.Count == 0, "其他平台 natives 不应显示为可导入：" + game);
        }
    });

    await Run("RAR 分析、导入及启停", async () =>
    {
        if (!File.Exists(rarFixture)) throw new SkipTestException("本机未找到 RAR 测试样本：" + rarFixture);
        var sandbox = Path.Combine(root, "rar-deploy");
        var game = CreateGame(sandbox, GameId.World);
        var archive = Path.Combine(sandbox, "rar-fixture.rar");
        File.Copy(rarFixture, archive);
        var analysis = await archiveService.AnalyzeAsync(archive, GameId.World);
        Check(analysis.Variants.Count > 0, "RAR 应识别至少一个 World 文件型版本");
        Check(analysis.Variants.SelectMany(v => v.Files).Any(f => f.Target.StartsWith("nativePC/", StringComparison.OrdinalIgnoreCase)), "RAR 应识别 nativePC 目标");
        var variant = analysis.Variants.First(v => v.BlockedReason is null && v.Files.Count > 0);
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var mod = await manager.ImportAsync(archive, GameId.World, game, variant.Name);
        await manager.SetEnabledAsync(mod.Id, true);
        Check(mod.Files.All(file => File.Exists(Path.Combine(game, file.Target.Replace('/', Path.DirectorySeparatorChar)))), "RAR 启用后应部署所选版本全部文件");
        await manager.SetEnabledAsync(mod.Id, false);
        Check(mod.Files.All(file => !File.Exists(Path.Combine(game, file.Target.Replace('/', Path.DirectorySeparatorChar)))), "RAR 停用后应撤回新增文件");
    });

    await Run("真实多版本 ZIP 导入与启停", async () =>
    {
        if (!File.Exists(multivariantFixture)) throw new SkipTestException("本机未找到多版本 ZIP 测试样本：" + multivariantFixture);
        var sandbox = Path.Combine(root, "real-multivariant");
        var game = CreateGame(sandbox, GameId.World);
        var archive = Path.Combine(sandbox, "multivariant.zip");
        File.Copy(multivariantFixture, archive);
        var analysis = await archiveService.AnalyzeAsync(archive, GameId.World);
        Check(analysis.Variants.Count >= 2, "应识别至少两个安装版本");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        foreach (var variant in analysis.Variants)
        {
            Check(variant.BlockedReason is null && variant.Files.Count > 0, "版本应可导入且包含文件：" + variant.Name);
            var mod = await manager.ImportAsync(archive, GameId.World, game, variant.Name);
            Check(!mod.Enabled && mod.Files.Count == variant.Files.Count, "导入后应保持停用并只记录所选版本");
            await manager.SetEnabledAsync(mod.Id, true);
            Check(mod.Files.All(file => File.Exists(Path.Combine(game, file.Target.Replace('/', Path.DirectorySeparatorChar)))), "启用后全部目标文件应存在：" + variant.Name);
            await manager.SetEnabledAsync(mod.Id, false);
            Check(mod.Files.All(file => !File.Exists(Path.Combine(game, file.Target.Replace('/', Path.DirectorySeparatorChar)))), "停用后应撤回新增文件：" + variant.Name);
        }
    });

    await Run("启用和停用恢复既有文件", async () =>
    {
        var sandbox = Path.Combine(root, "restore");
        var game = CreateGame(sandbox, GameId.World);
        var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "original");
        var archive = await CreateModArchiveAsync(sandbox, ".zip", "modded");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var mod = await manager.ImportAsync(archive, GameId.World, game);
        Check(!mod.Enabled, "导入后应保持停用");
        await manager.SetEnabledAsync(mod.Id, true);
        Check(File.ReadAllText(target) == "modded", "启用后应部署包内文件");
        await manager.SetEnabledAsync(mod.Id, false);
        Check(File.ReadAllText(target) == "original", "停用后应恢复原文件");
    });

    await Run("冲突切换", async () =>
    {
        var sandbox = Path.Combine(root, "switch");
        var game = CreateGame(sandbox, GameId.World);
        var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "baseline");
        var archiveA = await CreateModArchiveAsync(sandbox, ".zip", "a", "a");
        var archiveB = await CreateModArchiveAsync(sandbox, ".zip", "b", "b");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var a = await manager.ImportAsync(archiveA, GameId.World, game);
        var b = await manager.ImportAsync(archiveB, GameId.World, game);
        await manager.SetEnabledAsync(a.Id, true);
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(b.Id, true), "默认应拒绝覆盖冲突 Mod");
        await manager.SetEnabledAsync(b.Id, true, switchConflicts: true);
        Check(!manager.Mods.Single(x => x.Id == a.Id).Enabled, "切换后旧 Mod 应停用");
        Check(manager.Mods.Single(x => x.Id == b.Id).Enabled, "切换后新 Mod 应启用");
        Check(File.ReadAllText(target) == "b", "切换后目标文件应来自新 Mod");
        await manager.SetEnabledAsync(b.Id, false);
        Check(File.ReadAllText(target) == "baseline", "停用替换 Mod 后应还原原始文件");
    });

    await Run("冲突切换恢复旧包独有文件", async () =>
    {
        var sandbox = Path.Combine(root, "switch-exclusive");
        var game = CreateGame(sandbox, GameId.World);
        var existing = Path.Combine(game, "nativePC", "plugins", "existing.txt");
        var added = Path.Combine(game, "nativePC", "plugins", "added.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "original");
        var archiveA = await CreateZipAsync(Path.Combine(sandbox, "a.zip"),
            ("nativePC/plugins/shared.txt", "a"),
            ("nativePC/plugins/existing.txt", "replaced"),
            ("nativePC/plugins/added.txt", "added"));
        var archiveB = await CreateZipAsync(Path.Combine(sandbox, "b.zip"),
            ("nativePC/plugins/shared.txt", "b"));
        var data = Path.Combine(sandbox, "data");
        var manager = new ModManager(data, archiveService);
        var a = await manager.ImportAsync(archiveA, GameId.World, game);
        var b = await manager.ImportAsync(archiveB, GameId.World, game);
        await manager.SetEnabledAsync(a.Id, true);
        await manager.SetEnabledAsync(b.Id, true, switchConflicts: true);
        Check(File.ReadAllText(existing) == "original", "旧包独有的覆盖文件应恢复");
        Check(!File.Exists(added), "旧包独有的新增文件应撤回");
        var restarted = new ModManager(data, archiveService);
        Check(!restarted.Mods.Single(m => m.Id == a.Id).Enabled && restarted.Mods.Single(m => m.Id == b.Id).Enabled,
            "切换结果应持久保存");
        await restarted.SetEnabledAsync(b.Id, false);
        Check(!File.Exists(Path.Combine(game, "nativePC", "plugins", "shared.txt")), "新包停用后应恢复基线");
    });

    await Run("外部修改阻止停用覆盖", async () =>
    {
        var sandbox = Path.Combine(root, "external-change");
        var game = CreateGame(sandbox, GameId.World);
        var archive = await CreateModArchiveAsync(sandbox, ".zip", "managed");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var mod = await manager.ImportAsync(archive, GameId.World, game);
        await manager.SetEnabledAsync(mod.Id, true);
        var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
        File.WriteAllText(target, "external-edit");
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(mod.Id, false), "外部修改后停用应失败");
        Check(File.ReadAllText(target) == "external-edit", "外部文件修改必须保留");
        Check(manager.Mods.Single(x => x.Id == mod.Id).Enabled, "失败后记录应仍显示已启用");
    });

    await Run("停用基线被外部修改后拒绝重新启用", async () =>
    {
        var sandbox = Path.Combine(root, "baseline-external-change");
        var game = CreateGame(sandbox, GameId.World);
        var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "baseline");
        var archive = await CreateModArchiveAsync(sandbox, ".zip", "managed");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var mod = await manager.ImportAsync(archive, GameId.World, game);
        await manager.SetEnabledAsync(mod.Id, true);
        await manager.SetEnabledAsync(mod.Id, false);
        Check(File.ReadAllText(target) == "baseline", "停用后应恢复初始基线");
        File.WriteAllText(target, "external-baseline-edit");
        await Throws<InvalidOperationException>(() => manager.SetEnabledAsync(mod.Id, true), "基线被外部修改后应拒绝覆盖");
        Check(File.ReadAllText(target) == "external-baseline-edit", "拒绝重新启用时必须保留外部修改");
        Check(!manager.Mods.Single(x => x.Id == mod.Id).Enabled, "拒绝后 Mod 仍应记录为停用");
    });

    await Run("缓存包丢失后仍可安全停用", async () =>
    {
        var sandbox = Path.Combine(root, "missing-cache");
        var game = CreateGame(sandbox, GameId.World);
        var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "baseline");
        var archive = await CreateModArchiveAsync(sandbox, ".zip", "managed");
        var manager = new ModManager(Path.Combine(sandbox, "data"), archiveService);
        var mod = await manager.ImportAsync(archive, GameId.World, game);
        await manager.SetEnabledAsync(mod.Id, true);
        File.Delete(mod.ArchivePath);
        await manager.SetEnabledAsync(mod.Id, false);
        Check(File.ReadAllText(target) == "baseline", "缓存包丢失后应恢复基线");
    });

    await Run("多实例导入不会覆盖既有状态", async () =>
    {
        var sandbox = Path.Combine(root, "multi-instance");
        var game = CreateGame(sandbox, GameId.World);
        var data = Path.Combine(sandbox, "data");
        var archive = await CreateModArchiveAsync(sandbox, ".zip", "managed");
        var first = new ModManager(data, archiveService);
        var second = new ModManager(data, archiveService);
        await first.ImportAsync(archive, GameId.World, game);
        await second.ImportAsync(archive, GameId.World, game);
        first.Reload();
        Check(first.Mods.Count == 2, "跨实例保存前应重读最新状态");
    });

    await Run("拒绝危险压缩包路径", async () =>
    {
        var archive = Path.Combine(root, "traversal.zip");
        using (var stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("../escape.txt").Open());
            writer.Write("escape");
        }
        await Throws<InvalidDataException>(() => archiveService.AnalyzeAsync(archive, GameId.World), "目录穿越路径应在预览阶段拒绝");
    });

    await Run("拒绝大小写重名与损坏压缩包", async () =>
    {
        var duplicate = await CreateZipAsync(Path.Combine(root, "duplicate.zip"),
            ("nativePC/plugins/Mod.txt", "a"), ("nativePC/plugins/mod.txt", "b"));
        await Throws<InvalidDataException>(() => archiveService.AnalyzeAsync(duplicate, GameId.World), "大小写重名应拒绝");
        var broken = Path.Combine(root, "broken.zip");
        await File.WriteAllTextAsync(broken, "not an archive");
        await Throws<InvalidDataException>(() => archiveService.AnalyzeAsync(broken, GameId.World), "损坏包应拒绝");
    });

    await Run("拒绝加密压缩包", async () =>
    {
        var sandbox = Path.Combine(root, "encrypted");
        var game = CreateGame(sandbox, GameId.World);
        var source = Path.Combine(game, "nativePC", "plugins");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "secret.txt"), "fixture");
        var archive = Path.Combine(sandbox, "encrypted.7z");
        var created = await RunProcessAsync(sevenZip, ["a", "-t7z", "-pfixture-password", "-mhe=on", archive, @"nativePC\plugins\secret.txt"], game);
        Check(created.ExitCode == 0, "无法创建加密测试包：" + created.Error);
        await Throws<InvalidDataException>(() => archiveService.AnalyzeAsync(archive, GameId.World), "加密包应拒绝");
    });

    await Run("拒绝归档中的符号链接", async () =>
    {
        var archive = Path.Combine(root, "symlink.zip");
        using (var stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var link = zip.CreateEntry("nativePC/plugins/linked.txt");
            link.ExternalAttributes = unchecked((int)0xA1FF0000);
            await using var writer = new StreamWriter(link.Open());
            await writer.WriteAsync("../outside.txt");
        }
        var bytes = await File.ReadAllBytesAsync(archive);
        var central = -1;
        for (var i = 0; i < bytes.Length - 6; i++)
            if (bytes[i] == 0x50 && bytes[i + 1] == 0x4b && bytes[i + 2] == 0x01 && bytes[i + 3] == 0x02) { central = i; break; }
        Check(central >= 0, "符号链接测试包缺少 ZIP 中央目录");
        bytes[central + 5] = 3; // ZIP host OS: Unix，外部属性的文件类型位才有语义。
        await File.WriteAllBytesAsync(archive, bytes);
        await Throws<InvalidDataException>(() => archiveService.AnalyzeAsync(archive, GameId.World), "符号链接应在预览阶段拒绝");
    });

    await Run("启动时恢复未完成事务", async () =>
    {
        var sandbox = Path.Combine(root, "recovery");
        var game = CreateGame(sandbox, GameId.World);
        var data = Path.Combine(sandbox, "data");
        var archive = await CreateModArchiveAsync(sandbox, ".zip", "managed");
        var manager = new ModManager(data, archiveService);
        await manager.ImportAsync(archive, GameId.World, game);
        var target = Path.Combine(game, "nativePC", "plugins", "mod.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.CreateDirectory(Path.Combine(data, "transactions", "interrupted"));
        var backup = Path.Combine(data, "transactions", "interrupted", "00000.bak");
        var temporary = target + ".hmm-interrupted.tmp";
        File.WriteAllText(backup, "before");
        File.WriteAllText(target, "partially-written");
        var expectedAfterWrite = ArchiveService.HashFile(target);
        var stateBefore = File.ReadAllText(Path.Combine(data, "state.json"));
        var journal = new
        {
            BeforeState = stateBefore,
            Files = new[] { new { Path = target, Existed = true, Backup = backup, OriginalHash = ArchiveService.HashFile(backup), ExpectedHash = ArchiveService.HashFile(target), TemporaryPath = temporary } },
            WorkDirectory = Path.Combine(data, "transactions", "interrupted")
        };
        File.WriteAllText(Path.Combine(data, "pending-operation.json"), JsonSerializer.Serialize(journal, JsonSettings.Options));
        _ = new ModManager(data, archiveService);
        Check(File.ReadAllText(target) == "before", "启动应将文件恢复到事务开始状态");
        Check(!File.Exists(Path.Combine(data, "pending-operation.json")), "恢复后应清除未完成事务日志");

        var externalWork = Path.Combine(data, "transactions", "external-edit");
        Directory.CreateDirectory(externalWork);
        var externalBackup = Path.Combine(externalWork, "00000.bak");
        File.WriteAllText(externalBackup, "before");
        File.WriteAllText(target, "user-edit");
        var externalJournal = new
        {
            BeforeState = stateBefore,
            Files = new[] { new { Path = target, Existed = true, Backup = externalBackup, OriginalHash = ArchiveService.HashFile(externalBackup), ExpectedHash = expectedAfterWrite, TemporaryPath = target + ".hmm-external-edit.tmp" } },
            WorkDirectory = externalWork
        };
        File.WriteAllText(Path.Combine(data, "pending-operation.json"), JsonSerializer.Serialize(externalJournal, JsonSettings.Options));
        await Throws<InvalidOperationException>(() => Task.FromResult(new ModManager(data, archiveService)), "外部修改后恢复应停止");
        Check(File.ReadAllText(target) == "user-edit", "恢复拒绝后应保留外部修改");
        File.Delete(Path.Combine(data, "pending-operation.json"));

        var maliciousSandbox = Path.Combine(root, "malicious-recovery");
        var maliciousGame = CreateGame(maliciousSandbox, GameId.World);
        var maliciousData = Path.Combine(maliciousSandbox, "data");
        var maliciousArchive = await CreateModArchiveAsync(maliciousSandbox, ".zip", "managed");
        var maliciousManager = new ModManager(maliciousData, archiveService);
        await maliciousManager.ImportAsync(maliciousArchive, GameId.World, maliciousGame);
        var externalFile = Path.Combine(maliciousSandbox, "outside-game.txt");
        File.WriteAllText(externalFile, "external-safe");
        var maliciousWork = Path.Combine(maliciousData, "transactions", "interrupted");
        Directory.CreateDirectory(maliciousWork);
        var maliciousBackup = Path.Combine(maliciousWork, "00000.bak");
        File.WriteAllText(maliciousBackup, "attacker-content");
        var maliciousJournal = new
        {
            BeforeState = File.ReadAllText(Path.Combine(maliciousData, "state.json")),
            Files = new[] { new { Path = externalFile, Existed = true, Backup = maliciousBackup, OriginalHash = ArchiveService.HashFile(maliciousBackup), ExpectedHash = ArchiveService.HashFile(externalFile), TemporaryPath = externalFile + ".hmm-interrupted.tmp" } },
            WorkDirectory = maliciousWork
        };
        File.WriteAllText(Path.Combine(maliciousData, "pending-operation.json"), JsonSerializer.Serialize(maliciousJournal, JsonSettings.Options));
        await Throws<InvalidDataException>(() => Task.FromResult(new ModManager(maliciousData, archiveService)), "指向游戏目录外文件的恢复日志应被拒绝");
        Check(File.ReadAllText(externalFile) == "external-safe", "拒绝恶意恢复日志时必须保持外部文件不变");
    });
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

Console.WriteLine($"结果：{passed} 项通过，{failed.Count} 项失败");
foreach (var failure in failed) Console.WriteLine("FAIL " + failure);
if (failed.Count != 0) Environment.ExitCode = 1;

async Task Run(string name, Func<Task> test)
{
    try
    {
        await test();
        passed++;
        Console.WriteLine("PASS " + name);
    }
    catch (SkipTestException exception)
    {
        Console.WriteLine("SKIP " + name + " — " + exception.Message);
    }
    catch (Exception exception)
    {
        failed.Add(name + " — " + exception.Message);
        Console.WriteLine("FAIL " + name + " — " + exception.Message);
    }
}

static string CreateGame(string sandbox, GameId game)
{
    var path = Path.Combine(sandbox, "game");
    Directory.CreateDirectory(path);
    File.WriteAllText(Path.Combine(path, Games.Exe(game)), "fixture");
    return path;
}

static async Task<string> CreateZipAsync(string archivePath, params (string Path, string Contents)[] files)
{
    Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
    using var stream = File.Create(archivePath);
    using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
    foreach (var file in files)
    {
        var entry = zip.CreateEntry(file.Path);
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync(file.Contents);
    }
    return archivePath;
}

async Task<string> CreateModArchiveAsync(string sandbox, string extension, string contents, string? name = null)
{
    var id = name ?? Guid.NewGuid().ToString("N");
    var stage = Path.Combine(sandbox, "stage-" + id);
    var folder = Path.Combine(stage, "nativePC", "plugins");
    Directory.CreateDirectory(folder);
    await File.WriteAllTextAsync(Path.Combine(folder, "mod.txt"), contents);
    var archive = Path.Combine(sandbox, id + extension);
    if (extension == ".zip")
    {
        using var stream = File.Create(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("nativePC/plugins/mod.txt");
        await using var target = entry.Open();
        await using var source = File.OpenRead(Path.Combine(folder, "mod.txt"));
        await source.CopyToAsync(target);
    }
    else if (extension == ".7z")
    {
        var result = await RunProcessAsync(sevenZip, ["a", "-t7z", "-y", archive, @"nativePC\plugins\mod.txt"], stage);
        if (result.ExitCode != 0) throw new IOException("7-Zip fixture 创建失败：" + result.Error);
    }
    else throw new ArgumentOutOfRangeException(nameof(extension));
    return archive;
}

static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string executable, IEnumerable<string> args, string workingDirectory)
{
    var info = new ProcessStartInfo(executable) { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    foreach (var arg in args) info.ArgumentList.Add(arg);
    using var process = Process.Start(info) ?? throw new IOException("无法启动 7-Zip");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return (process.ExitCode, await stdout, await stderr);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task Throws<TException>(Func<Task> action, string message) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new InvalidOperationException(message);
}

sealed class SkipTestException(string message) : Exception(message);
