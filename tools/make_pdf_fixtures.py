"""Erzeugt die PDF-Testdateien unter tests/Egle360.Pdf.Tests/Fixtures.

Die Dateien werden eingecheckt; dieses Skript dokumentiert, wie sie entstanden sind.
Aufruf: python3 tools/make_pdf_fixtures.py  (benötigt pypdf)

Absichtlich nicht mit PDFium erzeugt, damit die Tests auch Dateien eines anderen Erzeugers lesen.
"""
import pathlib

from pypdf import PdfWriter
from pypdf.generic import DecodedStreamObject, NameObject

OUT = pathlib.Path(__file__).resolve().parent.parent / "tests" / "Egle360.Pdf.Tests" / "Fixtures"
MM = 72 / 25.4


def page_with_box(writer, width_pt, height_pt, rotate=0):
    """Seite mit schwarzem Quadrat 72 x 72 pt, 36 pt vom linken und unteren Rand."""
    page = writer.add_blank_page(width=width_pt, height=height_pt)
    stream = DecodedStreamObject()
    stream.set_data(b"0 0 0 rg 36 36 72 72 re f\n")
    page[NameObject("/Contents")] = writer._add_object(stream)
    if rotate:
        page.rotate(rotate)
    return page


def reference():
    w = PdfWriter()
    page_with_box(w, 841 * MM, 1189 * MM)            # A0 hoch
    page_with_box(w, 594 * MM, 420 * MM)             # A2 quer
    page_with_box(w, 210 * MM, 297 * MM, rotate=90)  # A4, um 90 Grad gedreht gespeichert
    page_with_box(w, 1000 * MM, 707 * MM)            # benutzerdefiniert
    w.add_metadata({
        "/Title": "Referenzplan EGLE360",
        "/Author": "EGLE360 Testdaten",
        "/Subject": "Testdatei für PDF-Grundfunktionen",
        "/CreationDate": "D:20261016120000+02'00'",
    })
    root = w.add_outline_item("Grundriss", 0)
    w.add_outline_item("Schnitt", 1, parent=root)
    w.add_outline_item("Details", 3)
    w.set_page_label(0, 1, prefix="GR-", style="/D")
    w.set_page_label(2, 3, style="/r")
    with open(OUT / "reference.pdf", "wb") as f:
        w.write(f)


def encrypted():
    w = PdfWriter()
    page_with_box(w, 210 * MM, 297 * MM)
    w.encrypt(user_password="egle360", owner_password="eigentuemer", algorithm="RC4-128")
    with open(OUT / "encrypted.pdf", "wb") as f:
        w.write(f)


def no_modify():
    """Ohne Kennwort lesbar, Änderungen laut Berechtigungen nicht erlaubt (nur Drucken)."""
    w = PdfWriter()
    page_with_box(w, 210 * MM, 297 * MM)
    w.encrypt(user_password="", owner_password="eigentuemer", algorithm="RC4-128", permissions_flag=0b0100)
    with open(OUT / "no-modify.pdf", "wb") as f:
        w.write(f)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    reference()
    encrypted()
    no_modify()
    (OUT / "not-a-pdf.pdf").write_text("Das ist kein PDF.\n", encoding="utf-8")
    print("Testdateien erzeugt.")
