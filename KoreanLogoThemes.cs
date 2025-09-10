using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

namespace KoreanLogoThemes;

public class KoreanLogoThemes : ModSystem
{
    private static readonly Dictionary<(string typeName, string propName), ILHook> logoILHookList = new();

    private Asset<Texture2D> originalLogo;

    public override void PostSetupContent()
    {
        var field = typeof(ModMenu).GetField("modLoaderLogo", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            originalLogo = (Asset<Texture2D>)field.GetValue(null);
            field?.SetValue(null, ModContent.Request<Texture2D>("KoreanLogoThemes/Assets/Textures/tModLoaderLogoKor", AssetRequestMode.ImmediateLoad));
        }

        var targets = new (string ModName, string TypeName, string PropName, string AssetPath)[]
        {
            //칼라미티
            ("CalamityMod", "CalamityMod.MainMenu.CalamityMainMenu", "Logo", "KoreanLogoThemes/Assets/Textures/CalamityLogoKor"),
            //칼라미티 모드 바닐라 음악 애드온
            ("UnCalamityModMusic", "UnCalamityModMusic.Content.Menus.ResurrectionMenu", "Logo", "KoreanLogoThemes/Assets/Textures/CalamityLogoResurrectionKor"),
            ("UnCalamityModMusic", "UnCalamityModMusic.Content.Menus.MemoryMenu", "Logo", "KoreanLogoThemes/Assets/Textures/CalamityLogoMemoryKor"),
            //인페르넘
            ("InfernumMode", "InfernumMode.Content.MainMenu.InfernumMainMenu", "Logo", "KoreanLogoThemes/Assets/Textures/InfernumLogoKor"),
            //카탈리스트
            ("CatalystMod", "CatalystMod.Content.MainMenus.AstrageldonStyle", "Logo", "KoreanLogoThemes/Assets/Textures/CatalystLogoKor"),
            //파르고
            ("FargowiltasSouls", "FargowiltasSouls.Content.UI.FargoMenuScreen", "Logo", "KoreanLogoThemes/Assets/Textures/FargoLogoKor"),
            ("FargowiltasSouls", "FargowiltasSouls.Content.UI.FargoMenuScreen", "LogoGlow", "KoreanLogoThemes/Assets/Textures/FargoLogoGlowKor")
        };

        foreach (var (modName, typeName, propName, assetPath) in targets)
        {
            if (ModLoader.TryGetMod(modName, out var mod))
            {
                ILHookLogo(mod, typeName, propName, assetPath);
            }
            else
            {
                Mod.Logger.Info($"Mod {modName} not found, skipping {typeName}.{propName}.");
            }
        }
    }

    private void ILHookLogo(Mod mod, string typeName, string propName, string assetPath)
    {
        var type = mod.Code.GetType(typeName);
        if (type == null)
        {
            Mod.Logger.Warn($"Type {typeName} not found in {mod.Name}.");
            return;
        }

        var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (prop == null)
        {
            Mod.Logger.Warn($"Property {propName} not found in {typeName}.");
            return;
        }

        var target = prop.GetGetMethod(nonPublic: true);
        if (target == null)
        {
            Mod.Logger.Warn($"Getter for {propName} not found in {typeName}.");
            return;
        }

        var logoILHook = new ILHook(target, ctx =>
        {
            var c = new ILCursor(ctx);
            c.EmitDelegate<Func<Asset<Texture2D>>>(() =>
            {
                return ModContent.Request<Texture2D>(assetPath, AssetRequestMode.ImmediateLoad);
            });
            c.Emit(OpCodes.Ret);
        });

        logoILHookList[(typeName, propName)] = logoILHook;
    }

    //for TML default
    private void ILHookTMLLogo()
    {
        var modMenuLogoGetter = typeof(ModMenu).GetProperty("Logo", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetGetMethod();

        if (modMenuLogoGetter != null)
        {
            var hook = new ILHook(modMenuLogoGetter, ctx =>
            {
                var c = new ILCursor(ctx);
                c.EmitDelegate<Func<Asset<Texture2D>>>(() =>
                {
                    return ModContent.Request<Texture2D>("KoreanLogoThemes/Assets/Textures/tModLoaderLogoKor", AssetRequestMode.ImmediateLoad);
                });
                c.Emit(OpCodes.Ret);
            });
            logoILHookList[("ModMenu", "Logo")] = hook;
        }
    }

    public override void Unload()
    {
        foreach (var logoILHook in logoILHookList.Values)
            logoILHook.Dispose();
        logoILHookList.Clear();

        var field = typeof(ModMenu).GetField("modLoaderLogo", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null && originalLogo != null)
        {
            field.SetValue(null, originalLogo);
        }
        originalLogo = null;
    }
}