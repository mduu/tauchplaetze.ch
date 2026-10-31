import importlib.util
import json
import pathlib
import unittest

HERE = pathlib.Path(__file__).parent
spec = importlib.util.spec_from_file_location("whatif_summary", HERE.parent / "whatif-summary.py")
whatif = importlib.util.module_from_spec(spec)
spec.loader.exec_module(whatif)


class WhatIfSummaryTests(unittest.TestCase):
    def setUp(self):
        self.text = whatif.summarize(json.loads((HERE / "whatif-fixture.json").read_text(encoding="utf-8")))

    def test_kopfzeile_zaehlt_nur_echte_aenderungen(self):
        self.assertIn("**Echt geändert: 3 Ressourcen**", self.text)
        self.assertIn("Neu: 1. Gelöscht: 1. Unverändert: 3.", self.text)

    def test_rauschen_steht_nur_im_ausgeklappten_teil(self):
        table, noise = self.text.split("<details><summary>Rauschen")
        self.assertNotIn("defaultEncryptionScope", table)
        self.assertNotIn("siteConfig.minTlsVersion", table)
        self.assertIn("defaultEncryptionScope", noise)
        self.assertIn("siteConfig.minTlsVersion", noise)

    def test_echte_aenderung_an_der_site_erscheint_mit_vorher_und_nachher(self):
        self.assertIn("`properties.httpsOnly` | `false` | `true`", self.text)
        self.assertIn("`properties.siteConfig.appSettings.0.value` | `alt` | `neu`", self.text)

    def test_geloeschte_ressource_wird_hervorgehoben(self):
        self.assertIn("⚠️ **Gelöscht:**", self.text)

    def test_geheimnisse_und_mailadressen_sind_maskiert(self):
        self.assertNotIn("Password=", self.text)
        self.assertNotIn("@example.test", self.text)
        self.assertIn("***", self.text)


if __name__ == "__main__":
    unittest.main()
