# Localization Rollout

The framework owns locale selection, persistence, stable text keys, fallback behavior,
the reusable builder, and validation. Each standalone game owns its locale assets,
String Tables, final translations, and font files under `Assets/Game/Localization`.

## Apply the rollout

1. Run `Setus > Horror Framework > Settings > Apply Phase 5 Localization Rollout`.
2. Import redistributable game fonts into `Assets/Game/Localization/Fonts`.
3. Open `GameLocaleFontProfile.asset` and configure every locale.
4. Use `Use Authored Font` only when every authored UI font contains the validator's
   probe glyphs. Otherwise assign a locale-specific `Font Override`.
5. Run `Setus > Horror Framework > Validation > Validate Phase 5 Localization Rollout`.

The builder creates missing locale and table assets without replacing existing table
GUIDs, shared entry IDs, authored translations, or font assignments. Missing game-specific
translations receive the authored English fallback so players never see a raw key; they
still require linguistic review before release.

## Font policy

Do not copy platform fonts such as PingFang, Microsoft YaHei, or Malgun Gothic into a
shipping project unless their license explicitly permits redistribution. Use licensed or
open fonts that cover:

- Latin Extended and Vietnamese diacritics for `vi` and `es`.
- Kana and the required kanji for `ja`.
- Hangul for `ko`.
- Simplified Chinese for `zh-Hans`.
- Traditional Chinese for `zh-Hant`.

The language menu exposes a locale only when its locale asset exists and its font profile
entry is configured. The editor validator additionally checks table completeness, content
scope, and representative glyphs.

## Release verification

Switch each locale from both Main Menu and Pause. Confirm text refreshes without closing
Settings or losing selection. Check 1920x1080 and 1280x720, then quit and relaunch to
verify the locale persisted independently of gameplay saves. Production Addressables and
the standalone player must be rebuilt after localization content changes.
