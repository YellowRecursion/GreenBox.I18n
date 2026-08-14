# GreenBox I18n branding

`greenbox-i18n-icon.png` and `greenbox-i18n-icon.svg` are the canonical raster and vector product
icons.

After replacing the source image, regenerate the committed platform assets:

```powershell
.\eng\Generate-BrandAssets.ps1
```

The script updates the Web favicon/header image, Windows application icon, Unity documentation,
Unity component icons, and the Rider plugin logos. Rider's generated SVGs use a 40×40 canvas and fit
the canonical vector into the recommended 36×36 visible area.
