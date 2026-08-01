# LCBridgeOverlay — сборка DLL (v1.0.0)

## Что нужно
- .NET SDK 6.0+ (https://dotnet.microsoft.com/download)
- Интернет для NuGet (первая сборка скачает зависимости)

## Сборка
1. Открой папку `LCBridgeOverlay` в терминале.
2. Выполни:
       dotnet build -c Release
3. Готовая DLL:
       bin/Release/LCBridgeOverlay.dll

NuGet сам подтянет BepInEx (в его состав входит HarmonyX) и игровые ссылки
(LethalCompany.GameLibs.Steam под V81: Assembly-CSharp, Unity.TextMeshPro,
Unity.InputSystem, uGUI и т.д.). Источники пакетов прописаны в nuget.config.
Если GameLibs не качается — как и в LCBridge, замени PackageReference на
ручные <Reference> к DLL из `Lethal Company_Data/Managed/` (нужны:
Assembly-CSharp, UnityEngine, UnityEngine.CoreModule, UnityEngine.UI,
UnityEngine.UIModule, UnityEngine.TextRenderingModule, Unity.TextMeshPro,
Unity.InputSystem).

## Ресурсы (шрифты, текстуры)

Папка ресурсов моду сейчас НЕ нужна:
- **Шрифт 3270** не встраивается — оверлей берёт готовый TMP-ассет
  «3270 SDF» из HUD самой игры (`HUDManager.chatText.font`). Это гарантирует
  пиксельное совпадение со стилем чата и избавляет от лицензионных вопросов.
  Для `Language=ru` создаётся динамический TMP-ассет из системного
  моноширинного шрифта (Consolas/Courier New) — в игровом 3270 нет кириллицы.
- **Текстуры** рамок/уголков рисуются кодом (цветные Image-прямоугольники,
  пиксельные уголки 26x5/5x26 как в HTML). Если в будущем понадобятся
  спрайты (иконки монстров и т.п.) — клади PNG в `res/` рядом с csproj,
  помечай их EmbeddedResource:
      <ItemGroup>
        <EmbeddedResource Include="res/**/*.png" />
      </ItemGroup>
  и загружай через Assembly.GetManifestResourceStream + Texture2D.LoadImage
  + Sprite.Create.

## Установка / проверка
1. `LCBridgeOverlay.dll` → `BepInEx/plugins/` (профиль r2modman).
   Рядом — LCBridge **1.2.0+** (протокол с полями beehiveCount, interiorType,
   hasOldBird, itemsInside/Outside, onShip и т.д.).
2. В логе BepInEx: «Harmony-патчи применены» и
   «Подключился к мосту LCBridge (ws://localhost:8181/)».
3. На корабле панель оверлея появляется справа по центру; `I` скрывает/показывает.
4. Выйди с корабля — панель укатывается вправо (~0.3 с, ease-out + fade).
5. С BCME: на луне видна плашка ивента в цветах BCME; при турельном ивенте
   (BerserkTurrets и т.п.) строка ловушек «стреляет» трассерами.
6. Сдай 3-ю квоту — баннер CHALLENGE COMPLETE с аналитикой забега.
7. `Style = Legacy` (или `UseLegacyStyle = true`) + перезапуск — старый
   пиксельный стиль с красными уголками.
