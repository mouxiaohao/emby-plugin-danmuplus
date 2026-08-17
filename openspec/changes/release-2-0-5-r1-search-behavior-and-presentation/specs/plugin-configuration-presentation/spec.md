## Purpose

Defines the DanmuPlus branding and canonical source destination shown inside the plugin configuration page without changing the compatibility identity used by Emby or existing installations.

## ADDED Requirements

### Requirement: Configuration page uses the DanmuPlus heading
The visible top heading inside the danmu configuration page SHALL be exactly `DanmuPlus 配置`.

#### Scenario: Administrator opens the configuration page
- **WHEN** Emby renders the plugin configuration page
- **THEN** its top heading SHALL display `DanmuPlus 配置` and SHALL NOT display `Danmu 配置`

### Requirement: Source link targets the DanmuPlus main branch
The configuration page's visible `源码` action SHALL link to `https://github.com/mouxiaohao/emby-plugin-danmuplus/tree/main` and SHALL continue opening as an external destination.

#### Scenario: Administrator follows the source action
- **WHEN** the administrator activates `源码`
- **THEN** the browser SHALL navigate to the DanmuPlus repository's `main` branch rather than an upstream or legacy repository

### Requirement: Compatibility identity remains unchanged
Changing the configuration-page heading and source link MUST NOT change the plugin assembly name, plugin identifier, Emby plugin-list display name, configuration-page route or generated resource keys, saved configuration model, update/release destination, or existing installation compatibility.

#### Scenario: Existing installation loads after the presentation change
- **WHEN** an installation with saved Danmu plugin configuration loads 2.0.5r1
- **THEN** Emby SHALL recognize the same plugin and configuration page, preserve all saved settings, and show unchanged plugin-list naming outside the page heading
