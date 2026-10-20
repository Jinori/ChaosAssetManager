using System.Diagnostics.CodeAnalysis;
using System.IO;
using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    private static PaletteLookup? KhanBPaletteLookup;
    private static PaletteLookup? KhanCPaletteLookup;
    private static PaletteLookup? KhanEPaletteLookup;
    private static PaletteLookup? KhanFPaletteLookup;
    private static PaletteLookup? KhanHPaletteLookup;
    private static PaletteLookup? KhanIPaletteLookup;
    private static PaletteLookup? KhanLPaletteLookup;
    private static PaletteLookup? KhanPPaletteLookup;
    private static PaletteLookup? KhanUPaletteLookup;
    private static PaletteLookup? KhanWPaletteLookup;
    private static IDictionary<int, Palette>? KhanMPaletteLookup;

    private static Animation? RenderKhanEpf(DataArchiveEntry entry, string archiveRoot)
    {
        var letter = entry.EntryName[1]
                          .ToString()
                          .ToLower();

        var male = entry.EntryName.StartsWithI("m");

        letter = letter switch
        {
            "a" => "b",
            "g" => "c",
            "j" => "c",
            "o" => "m",
            "s" => "p",
            _   => letter
        };

        //bodies
        if (letter.EqualsI("m"))
            return RenderKhanMEpf(entry, archiveRoot);

        //pants
        if (letter.EqualsI("n"))
            return RenderKhanNEpf(entry, archiveRoot);

        if (!TryLoadKhanPaletteLookup(archiveRoot, letter, out var lookup))
            return null;

        //lod armor sheets use 7 digit ids (mu0000389.epf, mu000038901.epf)
        var numDigits = Path.GetFileNameWithoutExtension(entry.EntryName)
                            .Count(char.IsDigit) >= 7
            ? 7
            : 3;

        if (!entry.TryGetNumericIdentifier(out var identifier, numDigits))
            return null;

        var overrideType = male ? KhanPalOverrideType.Male : KhanPalOverrideType.Female;
        var (palette, alphaType) = lookup.GetPaletteAndAlphaType(identifier, overrideType);
        var epfFile = EpfFile.FromEntry(entry);
        var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette, alphaType));
        var images = new SKImageCollection(transformer);

        return new Animation(images, 200);
    }

    private static Animation? RenderKhanMEpf(DataArchiveEntry entry, string archiveRoot)
    {
        if (!TryLoadKhanMPaletteLookup(archiveRoot, out var lookup))
            return null;

        var palettes = lookup.Values;
        var epfFile = EpfFile.FromEntry(entry);

        //we're rendering the frames in a grid, but those frames might have different placements / widths / heights
        var maxWidth = epfFile.Max(frame => frame.PixelWidth + frame.Left);
        var maxHeight = epfFile.Max(frame => frame.PixelHeight + frame.Left);

        var transformer = epfFile.Select(frame =>
        {
            //so we add padding to each frame so that all frames line up in the grid during the animation
            var paddingX = maxWidth - (frame.PixelWidth + frame.Left) + 2;
            var paddingY = maxHeight - (frame.PixelHeight + frame.Left) + 2;

            var transformer2 = palettes.Select(palette => Graphics.RenderImage(frame, palette));
            using var images = new SKImageCollection(transformer2);

            return CreateGrid(images, paddingX, paddingY);
        });
        var images = new SKImageCollection(transformer);

        return new Animation(images, 200);
    }

    private static Animation? RenderKhanNEpf(DataArchiveEntry entry, string archiveRoot)
    {
        if (!TryLoadDyePalettes(archiveRoot, out var lookup))
            return null;

        var epfFile = EpfFile.FromEntry(entry);

        //we're rendering the frames in a grid, but those frames might have different placements / widths / heights
        var maxWidth = epfFile.Max(frame => frame.PixelWidth + frame.Left);
        var maxHeight = epfFile.Max(frame => frame.PixelHeight + frame.Left);

        var transformer = epfFile.Select(frame =>
        {
            //so we add padding to each frame so that all frames line up in the grid during the animation
            var paddingX = maxWidth - (frame.PixelWidth + frame.Left) + 2;
            var paddingY = maxHeight - (frame.PixelHeight + frame.Left) + 2;

            //pants use dye colors, but only 0-15
            //after 15, the color would interfere with the body shape, so it's impossible
            var transformer2 = Enumerable.Range(0, 16)
                                         .Select(dyeIndex =>
                                         {
                                             //we want to create 1 image in the grid per dye color
                                             var palette = lookup[dyeIndex];

                                             return Graphics.RenderImage(frame, palette);
                                         });

            using var images = new SKImageCollection(transformer2);

            return CreateGrid(images, paddingX, paddingY);
        });

        var images = new SKImageCollection(transformer);

        return new Animation(images, 200);
    }

    private static bool TryLoadKhanMPaletteLookup(string archiveRoot, [NotNullWhen(true)] out IDictionary<int, Palette>? lookup)
    {
        //these palettes have no table, and represent the different body colors
        //the numeric identifiers of the palettes line up with the numeric values for body colors in the game
        lookup = KhanMPaletteLookup;

        if (lookup is not null)
            return true;

        if (!TryOpenKhanPalArchive(archiveRoot, out var kahnPal))
            return false;

        using var _ = kahnPal;
        lookup = KhanMPaletteLookup = Palette.FromArchive("palm", kahnPal);

        return true;
    }

    private static bool TryLoadKhanPaletteLookup(string archiveRoot, string letter, [NotNullWhen(true)] out PaletteLookup? lookup)
    {
        letter = letter.ToLower();
        var khanPal = new Lazy<DataArchive?>(() => TryOpenKhanPalArchive(archiveRoot, out var archive) ? archive : null);

        PaletteLookup? Load()
            => khanPal.Value is { } archive
                ? PaletteLookup.FromArchive($"pal{letter}", archive)
                               .Freeze()
                : null;

        try
        {
            lookup = letter switch
            {
                "b" => KhanBPaletteLookup ??= Load(),
                "c" => KhanCPaletteLookup ??= Load(),
                "e" => KhanEPaletteLookup ??= Load(),
                "f" => KhanFPaletteLookup ??= Load(),
                "h" => KhanHPaletteLookup ??= Load(),
                "i" => KhanIPaletteLookup ??= Load(),
                "l" => KhanLPaletteLookup ??= Load(),
                "p" => KhanPPaletteLookup ??= Load(),
                "u" => KhanUPaletteLookup ??= Load(),
                "w" => KhanWPaletteLookup ??= Load(),
                _   => null
            };

            return lookup != null;
        } finally
        {
            if (khanPal.IsValueCreated)
                khanPal.Value?.Dispose();
        }
    }

    /// <summary>
    ///     Opens the archive that holds the khan palettes. Unora keeps them in khanpal.dat, Lod keeps them inside khan.dat
    /// </summary>
    private static bool TryOpenKhanPalArchive(string archiveRoot, [NotNullWhen(true)] out DataArchive? archive)
    {
        var khanPalPath = Path.Combine(archiveRoot, "khanpal.dat");

        if (File.Exists(khanPalPath))
        {
            archive = DataArchive.FromFile(khanPalPath);

            return true;
        }

        var khanPath = Path.Combine(archiveRoot, "khan.dat");

        if (File.Exists(khanPath))
        {
            archive = DataArchive.FromFile(khanPath);

            if (archive.Contains("palm001.pal"))
                return true;

            archive.Dispose();
        }

        archive = null;

        return false;
    }
}