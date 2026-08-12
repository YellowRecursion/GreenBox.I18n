# GreenBox I18n branding

`greenbox-i18n-icon.png` is the current canonical product icon. It is intentionally temporary and
may be replaced later without changing application code.

After replacing the source image, regenerate the committed platform assets:

```powershell
.\eng\Generate-BrandAssets.ps1
```

The script updates the Web favicon/header image and the Windows application icon. Rider requires a
small vector logo in `Ide/Rider/src/main/resources/META-INF`, so its SVG files are maintained as a
careful vector adaptation of the same artwork.
