# Release UI Polish Design Spec

Date: 2026-05-10

## Status

Ready for user review.

## Context

MusicBakh уже имеет базовую продуктовую оболочку и второй пакет интерактивного плеера: play/pause, seek, skip, prev/next, volume, mute, repeat, hotkeys, persistence. Перед первой релизной сборкой остались визуальные шероховатости, которые сразу бросаются в глаза:

- системные WPF scrollbars выбиваются из тёмно-золотой темы;
- seek-слайдер выбранного/играющего трека ведёт себя визуально неоднозначно при выборе другого трека;
- repeat label наезжает на иконку;
- seek/volume sliders выглядят системно и слабее старого ProgressBar.

Этот пакет не добавляет новые функции. Его цель — сделать уже существующее управление воспроизведением визуально цельным и предсказуемым перед релизом.

## Package Goal

Довести текущий интерфейс MusicBakh до релизного визуального уровня: тонкие тематические scrollbars, аккуратные seek/volume sliders, корректное отображение player bar только для фактически выбранного играющего трека, и исправленный repeat indicator.

## Scope

### In scope

- Глобальные стили для вертикальных и горизонтальных `ScrollBar`.
- Применение scrollbar-стиля ко всем текущим зонам прокрутки:
  - библиотека треков;
  - центральная панель выбранного трека;
  - история прослушиваний;
  - popup списка жанров;
  - любые системные scrollbars, появляющиеся внутри существующих `ScrollViewer` / `ListBox` / `ComboBox`.
- Скрытие seek/time/player-progress блока, когда выбранный трек не совпадает с `PlayingTrack`.
- Сохранение badge `играет: ...`, когда играет другой трек.
- Стилизация `Slider` для seek и volume под текущую MusicBakh тему.
- Исправление repeat label: `Off` / `1` / `All` не должны наезжать на иконку повтора.
- Минимальная визуальная стабилизация отступов в центральной колонке после замены ProgressBar на player bar.
- Проверка на минимальном размере окна и в fullscreen/maximized.

### Out of scope

- Новая app icon / taskbar icon. Это отдельный пакет `MusicBakh Visual Identity & App Icon`.
- Figma-макеты. Для этого пакета достаточно текстовой спеки и визуального smoke.
- Новые playback features.
- Новая логика очереди, shuffle, lyrics, NAudio pulse.
- Изменение цветовой палитры бренда.
- Переписывание layout всей главной страницы.
- Удаление `docs/superpowers`, `.claude`, README, installer. Это следующие релизные пакеты.

## Design Decisions

### 1. Scrollbars

Добавить отдельный resource dictionary:

```text
MusicLibrary/Resources/ScrollBarStyles.xaml
```

Подключить его в `App.xaml` после brushes/colors и до control templates, чтобы стили были доступны `ListBox`, `ScrollViewer`, `ComboBox`.

Scrollbars должны быть:

- тонкие: 8px вертикальные, 8px горизонтальные;
- без стрелочных кнопок;
- track почти прозрачный (`#0DFFFFFF` или близкий к `CardBrush`);
- thumb золотистый/полупрозрачный в normal state;
- thumb ярче на hover/drag;
- скругление thumb 4px;
- без белых системных областей;
- без layout shift при hover.

Стиль должен покрывать `Orientation=Vertical` и `Orientation=Horizontal`. В WPF это удобнее сделать одним implicit style для `ScrollBar` с orientation triggers или двумя внутренними templates через triggers.

Инвариант: scrollbars не должны становиться шире при hover и не должны менять размеры списков.

### 2. Seek Visibility

Текущее поведение: если играет трек A, пользователь выбирает трек B, элементы seek/time могут продолжать показывать позицию трека A в панели трека B. Это визуально выглядит как будто позиция относится к выбранному треку B.

Нужное поведение:

- seek slider и time labels показываются только когда `IsSelectedPlaying == true`;
- когда выбран другой трек, seek/time скрываются;
- при этом transport controls могут оставаться видимыми, потому что Play/Pause/Next/Volume/Repeat — это общие controls плеера;
- badge `играет: Artist — Title` остаётся источником правды для ситуации “выбран одно, играет другое”.

Практическое изменение:

- выделить seek/time row в отдельный контейнер;
- привязать `Visibility` к `IsSelectedPlaying`;
- не скрывать Save/Delete/status и не ломать existing player controls.

Инвариант: при выборе другого трека playback не останавливается, `PlayingTrack` не меняется, история не меняется.

### 3. Repeat Indicator

Проблема: label `Off` / `1` / `All` расположен слишком близко к repeat icon и может визуально наезжать на неё.

Нужное поведение:

- иконка repeat и label должны восприниматься как один compact control;
- label располагается ниже или ниже-правее иконки, без пересечения;
- для `Off`, `1`, `All` должен быть стабильный контейнер, чтобы кнопка не прыгала при переключении режима;
- активные режимы `Current` и `Library` остаются золотыми, `Off` приглушённый.

Рекомендуемый layout внутри repeat button:

```text
┌──────┐
│ icon │
│ txt  │
└──────┘
```

То есть вертикальный `Grid`/`StackPanel` внутри кнопки: icon сверху, label снизу, `FontSize=10-11`, `Margin=0,2,0,0`.

### 4. Slider Styling

Seek и volume должны выглядеть как часть MusicBakh, а не как системные WPF controls.

Добавить resource dictionary:

```text
MusicLibrary/Resources/SliderStyles.xaml
```

Или, если реализация получится маленькой, добавить стили в существующий `ButtonStyles.xaml` не нужно; лучше отдельный файл, потому что это не кнопки.

Стили:

- `PlayerSliderStyle` — общий базовый стиль для `Slider`.
- `SeekSliderStyle` — full-width seek.
- `VolumeSliderStyle` — compact width volume.

Визуальные требования:

- track height 6px для seek, 5px для volume;
- background track: `AccentBrush` или полупрозрачный `MutedForegroundBrush`;
- filled/decrease часть: `PrimaryBrush` / `GoldGradientBrush`;
- thumb: круг 14–16px, `PrimaryBrush`, лёгкая тень;
- hover thumb чуть ярче/крупнее визуально, но без изменения layout;
- disabled seek: track приглушён, thumb opacity 0.35;
- никаких системных синих/белых цветов;
- value fill должен показывать текущую позицию/громкость, не только thumb.

WPF `Slider` требует custom `ControlTemplate` с `Track`, `DecreaseRepeatButton`, `IncreaseRepeatButton`, `Thumb`. Для fill-прогресса использовать background у `DecreaseRepeatButton` и прозрачный `IncreaseRepeatButton`.

Инвариант: `SeekSlider.Value` остаётся `Mode=OneWay`, seek write идёт через `SeekToCommand` в code-behind mouse-up handler.

## Architecture

### Files created

```text
MusicLibrary/Resources/ScrollBarStyles.xaml
MusicLibrary/Resources/SliderStyles.xaml
```

### Files modified

```text
MusicLibrary/App.xaml
MusicLibrary/MainWindow.xaml
MusicLibrary/Resources/ComboBoxStyles.xaml
```

`ComboBoxStyles.xaml` может потребоваться, если popup жанров использует собственный `ScrollViewer` и не подхватывает implicit scrollbar style из application resources. Если implicit style работает, файл не трогать.

### Optional files

```text
MusicLibrary/Resources/ListStyles.xaml
```

Трогать только если `ListBox` templates явно переопределяют scrollbars и мешают global style.

## Behavioral Invariants

- `SelectedTrack` и `PlayingTrack` остаются разделёнными.
- Смена выбранного трека не останавливает playback.
- Смена жанра не останавливает playback.
- Player controls не должны менять command behavior.
- Seek работает только через `SeekToCommand`; XAML не должен пытаться писать напрямую в read-only `ProgressValue`.
- Volume slider продолжает писать в `MainViewModel.Volume`.
- Repeat button продолжает циклировать `Off -> Current -> Library -> Off`.
- Удаление Stop button уже выполнено в предыдущем пакете; этот пакет не возвращает Stop.
- Все новые XAML-комментарии — на русском.

## Failure Patterns

- **Белые системные scrollbars остались в ComboBox popup.** Проверить dropdown жанров отдельно; WPF popup иногда требует style внутри template.
- **Scrollbar thumb слишком тонкий для мыши.** Минимум 8px; меньше будет красиво, но неудобно.
- **Slider fill не виден.** Значит `DecreaseRepeatButton`/`IncreaseRepeatButton` template не отражает value.
- **Seek slider снова TwoWay.** Это вызовет binding noise, потому что `ProgressValue` getter-only.
- **Seek отображается для выбранного неиграющего трека.** Проверить сценарий: запустить A, выбрать B, seek/time должны исчезнуть, badge “играет” должен остаться.
- **Repeat label прыгает при смене Off/1/All.** Нужен стабильный width/min-width.
- **Hover меняет размеры controls.** Hover может менять цвет/opacity/effect, но не width/height/margin.
- **Стили ломают disabled controls.** Disabled prev/next/seek должны быть видимо disabled, но не исчезать внезапно.

## Testing And Verification

### Automated

Минимальный обязательный прогон:

```powershell
dotnet build .\MusicLibrary.sln --no-restore
dotnet test .\MusicLibrary.sln --no-restore
dotnet build .\MusicLibrary.sln -c Release --no-restore
```

Unit tests, скорее всего, не нужны, если не меняется ViewModel behavior. Если придётся добавить converter/trigger helper, покрыть его тестом.

### Manual visual smoke

Проверить:

1. Открыть приложение в обычном размере.
2. Проверить библиотеку треков: scrollbar тонкий, тёмный, без белого системного фона.
3. Проверить историю: scrollbar соответствует теме.
4. Уменьшить высоту окна до появления scrollbar в центральной панели: он соответствует теме.
5. Открыть dropdown жанров: scrollbar соответствует теме.
6. Запустить трек A.
7. Выбрать трек B: seek/time скрылись, badge “играет: A” остался.
8. Выбрать обратно A: seek/time вернулись и показывают позицию A.
9. Переключить repeat Off/1/All: label не наезжает на иконку, кнопка не прыгает.
10. Подвигать volume slider: выглядит в теме, thumb/fill корректные.
11. Подвигать seek slider: выглядит в теме, seek работает после отпускания.
12. Проверить fullscreen/maximized.

## Release Acceptance Criteria

- В приложении не осталось видимых белых/системных scrollbars на основных путях.
- Seek/time не показываются для выбранного трека, который не является `PlayingTrack`.
- Repeat indicator читабелен во всех трёх режимах.
- Seek и volume sliders визуально совпадают с MusicBakh palette.
- Build/test/release build зелёные.
- Нет изменений за пределами UI polish scope.

## Work Diff

`MusicLibrary/work_diff.md` обновлять не обязательно: этот пакет не меняет функциональность относительно работы, а только полирует внешний вид уже описанных interactive controls. Если в реализации появится поведенческое изменение, добавить короткий пункт в существующий раздел 11.

## Next Package

После этого пакета идти к `MusicBakh Visual Identity & App Icon`: финальная иконка приложения, taskbar icon, `.ico`, возможно исходники в `docs/assets`.
