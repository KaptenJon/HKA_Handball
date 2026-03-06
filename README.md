# HKA Handball 🏐

A free, offline handball game built with .NET MAUI.

## Features

- 🎮 **Touch & keyboard controls** – joystick, pass, shoot, defend
- 🏐 **Handball rules** – goal area, free throws, goalkeeper saves
- 📱 **Fully offline** – no internet, no ads, no data collection
- 🆓 **100% free** – open source, no in-app purchases

## Download

[![Google Play](https://img.shields.io/badge/Google%20Play-Download-green?logo=google-play)](https://play.google.com/store/apps/details?id=com.kaptenjon.hkahandball)

## Build

```bash
# Debug (Android)
dotnet build -f net10.0-android

# Release AAB for Google Play
dotnet publish -f net10.0-android -c Release \
  -p:AndroidSigningStorePass=YOUR_PASSWORD \
  -p:AndroidSigningKeyPass=YOUR_PASSWORD
```

### First-time setup

1. Generate a signing keystore: `pwsh generate-keystore.ps1`
2. Keep the `.keystore` file safe – you need it for every Play Store update

## Privacy

This app collects **no data**. See [Privacy Policy](PRIVACY_POLICY.md).

## Security

To report a vulnerability, please see [Security Policy](SECURITY.md).