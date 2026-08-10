# GreenBox I18n project files

This folder is created and maintained automatically by GreenBox I18n.

## Folder contract

- You may move this whole folder anywhere inside `Assets`. Move it through Unity so its `.meta` file is preserved.
- Keep the `GreenBox.I18n` folder name. The runtime does not depend on it, but tools and developers use it to recognize the folder.
- Do not rename, move, replace, or delete files and folders inside it. GreenBox I18n validates and repairs the managed runtime layout.

## Files

- `localization.json` is the editable source of truth. Prefer the GreenBox Web Editor or CLI.
- `Resources/greenbox-i18n.asset` is generated runtime data and is excluded from Git. Do not edit or reference it manually.
- `.gitignore` and `.gitattributes` keep generated data out of Git and normalize text files across operating systems. Commit both files to your project repository.
- If you must edit `localization.json` manually, preserve existing entry IDs and validate the result afterwards.
