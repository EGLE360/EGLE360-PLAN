"""Erzeugt WPF-Ressourcen und CSS-Variablen aus design/tokens.json.

Aufruf aus dem Repository-Stamm: python3 tools/generate_theme.py
Die erzeugten Dateien werden eingecheckt; der Test Egle360.Design.Tests prüft,
dass sie zum aktuellen tokens.json passen.
"""
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
TOKENS = json.loads((ROOT / "design" / "tokens.json").read_text(encoding="utf-8"))
THEMES_DIR = ROOT / "src" / "Egle360.Desktop" / "Themes"
HEADER = "<!-- Automatisch erzeugt aus design/tokens.json durch tools/generate_theme.py. Nicht von Hand ändern. -->"
NS = ('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n'
      '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n'
      '                    xmlns:sys="clr-namespace:System;assembly=mscorlib">')


def color_block(colors):
    lines = []
    for key, hex_ in colors.items():
        argb = "#FF" + hex_[1:].upper()
        lines.append(f'    <Color x:Key="{key}Color">{argb}</Color>')
        lines.append(f'    <SolidColorBrush x:Key="{key}Brush" Color="{argb}" />')
    return lines


LOGO_DECODE_HEIGHT = 512


def logo_image(key, file_name, decode=True):
    # Die Datei bleibt unverändert; DecodePixelHeight begrenzt nur den Speicher beim Anzeigen.
    uri = f"pack://application:,,,/EGLE360.PLAN;component/Assets/{file_name}"
    limit = f' DecodePixelHeight="{LOGO_DECODE_HEIGHT}"' if decode else ""
    return [f'    <BitmapImage x:Key="{key}" UriSource="{uri}"{limit} CacheOption="OnLoad" />']


def logo_name(file_name):
    return file_name.removeprefix("EGLE360_").removesuffix(".png").removesuffix("_8192").replace("_", " ")


def write(path, lines):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def brand_xaml():
    lines = [HEADER, NS, "    <!-- Logofarben -->"]
    lines += color_block(TOKENS["logo"])
    lines.append("    <!-- Markenfarben -->")
    lines += color_block(TOKENS["brand"])
    lines.append("    <!-- Navigation (in beiden Modi dunkel) -->")
    lines += color_block(TOKENS["navigation"])
    lines.append("    <!-- Logodateien (unverändert aus brand/) -->")
    lines += logo_image("LogoNavigationImage", TOKENS["logoUsage"]["Navigation"])
    lines += logo_image("LogoReportImage", TOKENS["logoUsage"]["Report"], decode=False)
    t = TOKENS["typography"]
    lines.append("    <!-- Typografie -->")
    lines.append(f'    <FontFamily x:Key="FontFamilyUi">{t["FontFamily"]}</FontFamily>')
    lines.append(f'    <FontFamily x:Key="FontFamilyMono">{t["FontFamilyMono"]}</FontFamily>')
    for k in ("SizeCaption", "SizeBody", "SizeSubtitle", "SizeTitle", "SizeDisplay"):
        lines.append(f'    <sys:Double x:Key="Font{k}">{t[k]}</sys:Double>')
    lines.append("    <!-- Abstände und Radien -->")
    for k, v in TOKENS["spacing"].items():
        lines.append(f'    <sys:Double x:Key="Space{k}">{v}</sys:Double>')
        lines.append(f'    <Thickness x:Key="Pad{k}">{v}</Thickness>')
    for k, v in TOKENS["radius"].items():
        lines.append(f'    <CornerRadius x:Key="Radius{k}">{v}</CornerRadius>')
    lines.append("</ResourceDictionary>")
    write(THEMES_DIR / "Brand.xaml", lines)


def theme_xaml(name):
    lines = [HEADER, NS, f"    <!-- {name} Mode -->"]
    lines += color_block(TOKENS["themes"][name])
    lines.append("    <!-- Logo für Inhaltsflächen dieses Modus -->")
    lines += logo_image("LogoContentImage", TOKENS["logoUsage"][name])
    lines.append(f'    <sys:String x:Key="LogoContentName">{logo_name(TOKENS["logoUsage"][name])}</sys:String>')
    lines.append("</ResourceDictionary>")
    write(THEMES_DIR / f"Colors.{name}.xaml", lines)


def css():
    def kebab(s):
        out = ""
        for i, ch in enumerate(s):
            if ch.isupper() and i:
                out += "-"
            out += ch.lower()
        return out.replace("surface-1", "surface1").replace("surface-2", "surface2")

    def block(colors):
        return "\n".join(f"  --egle-{kebab(k)}: {v};" for k, v in colors.items())

    fixed = {**TOKENS["logo"], **TOKENS["brand"], **TOKENS["navigation"]}
    text = f"""/* Automatisch erzeugt aus design/tokens.json durch tools/generate_theme.py. Nicht von Hand ändern. */
:root {{
{block(fixed)}
{block(TOKENS["themes"]["Light"])}
}}
@media (prefers-color-scheme: dark) {{
  :root:not([data-theme="light"]) {{
{block(TOKENS["themes"]["Dark"])}
  }}
}}
:root[data-theme="dark"] {{
{block(TOKENS["themes"]["Dark"])}
}}
"""
    (ROOT / "design" / "tokens.css").write_text(text, encoding="utf-8")


if __name__ == "__main__":
    brand_xaml()
    theme_xaml("Light")
    theme_xaml("Dark")
    css()
    print("Themes erzeugt.")
