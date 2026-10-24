"""Build native static faces from the checked-in, OFL-licensed source fonts.

Requires fonttools 4.60.1. This is an asset build tool, not an application dependency.
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

fonts = Path(__file__).resolve().parents[1] / "Lucia.Companion.App" / "Assets" / "Fonts"
for family in ("Outfit", "DMSans"):
    for style, weight in (("Regular", 400), ("SemiBold", 600)):
        source = TTFont(fonts / f"{family}.ttf")
        axes = {axis.axisTag: axis.defaultValue for axis in source["fvar"].axes}
        axes["wght"] = weight
        if "opsz" in axes:
            axes["opsz"] = 14
        instance = instantiateVariableFont(source, axes, inplace=True, updateFontNames=True)
        instance.save(fonts / f"{family}-{style}.ttf")
