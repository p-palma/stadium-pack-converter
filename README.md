# Stadium Pack Converter

A small Windows app for the **Kojuro skin** for Football Manager 27. It turns a stadium picture
pack into the format the skin's club page reads, so every club shows its own ground.

**[Download stadium-pack-converter.exe](https://github.com/p-palma/stadium-pack-converter/releases/latest/download/stadium-pack-converter.exe)**
(one file, nothing to install, Windows only)

Get the skin: https://ko-fi.com/s/2817643952

## How to use it (with FM closed)

1. Download a stadium picture pack and unzip it anywhere. We recommend the free
   [Mini Stadiums Superpack](https://www.fmscout.com/a-mini-stadiums-superpack-fm24.html) by stevemc.
2. Double-click `stadium-pack-converter.exe`.
   If Windows shows "Windows protected your PC", click **More info > Run anyway**
   (the app is free and not signed, so Windows doesn't know it yet).
3. **Your stadium pack folder**: click *Choose...* and pick the pack's folder.
4. **Save to** is already filled in: `Documents\Sports Interactive\Football Manager 27\graphics\Stadium Pack`. Keep it.
5. Click **Convert Stadium Pack**. After a few seconds it shows how many clubs have a photo.
6. Start FM27 and open a club page.

Using several packs? Put them in one folder and choose that folder. Added or removed a pack? Run it again.
To remove the photos, delete `graphics\Stadium Pack`.

## What it does

- Reads every `config.xml` in the folder you choose (packs inside packs too) and takes each
  `pictures/club/<id>/stadium` line (`team/<id>` only as a fallback). Folders named *stadium* with
  loose `<club id>.jpg` files and no config work too.
- Copies each photo as `stadium_<club id>.<ext>` into the Save to folder and writes one `config.xml`
  for the skin. The first pack in the list wins a club.
- Photos linked to a stadium instead of a club can't be used: FM27 doesn't tell the skin which
  stadium a club plays in. They are only counted.
- It only reads your packs and never changes them. It writes only into the Save to folder,
  needs no admin rights and no internet.

## Build it yourself

No Visual Studio needed: `build.bat` uses the C# compiler that ships with Windows (.NET Framework 4)
and embeds `CREDITS.txt`.

```
build.bat
```

Test mode without the window:
`stadium-pack-converter.exe --source <pack folder> --out <folder> [--report <file>]`

## Licence

MIT (see `LICENSE`). Stadium photos from packs belong to their authors: please credit them.
Free and unofficial, by twitch.tv/kojuropp.
