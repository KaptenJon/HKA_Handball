# Releasing HKA Handball to Google Play

Step-by-step guide to sign, build, and publish the app to Google Play.

---

## 1. Generate a Signing Key

Google Play requires every app to be signed with a cryptographic key. You generate
a **keystore** file once and reuse it for every update.

> **⚠️ Keep your keystore safe!** If you lose it, you cannot push updates to the
> same Play Store listing. Back it up somewhere secure (e.g., a password manager
> or encrypted drive).

### Option A – Bash / macOS / Linux

```bash
bash generate-keystore.sh
```

You will be prompted for a password. Use a strong password and save it somewhere
secure.

### Option B – PowerShell / Windows

```powershell
pwsh generate-keystore.ps1
```

The script will prompt you for a strong password at runtime. Do **not** hard‑code
the password into the script file.

Alternatively, you can run `keytool` manually (omitting `-storepass`/`-keypass`
so it prompts you securely for the passwords instead of taking them on the
command line):

```bash
keytool -genkeypair \
  -v \
  -keystore HKA_Handball/hkahandball.keystore \
  -alias hkahandball \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000 \
  -dname "CN=HKA Handball, O=KaptenJon, L=Sweden, C=SE"
```

After running any of the above, the file `HKA_Handball/hkahandball.keystore` is
created. The `.gitignore` already prevents it from being committed.

---

## 2. Add the Signing Key to GitHub Actions

The release workflow (`.github/workflows/release.yml`) builds a signed AAB using
three repository **secrets**. Add them in your GitHub repo under
**Settings → Secrets and variables → Actions → New repository secret**.

| Secret name                  | Value                                                                 |
| ---------------------------- | --------------------------------------------------------------------- |
| `ANDROID_KEYSTORE_BASE64`    | Base64-encoded contents of `hkahandball.keystore` (see below)         |
| `ANDROID_KEYSTORE_PASSWORD`  | The store password you chose when generating the keystore             |
| `ANDROID_KEY_PASSWORD`       | The key password (usually the same as the store password)             |

### How to Base64-encode the keystore

```bash
# Linux (GNU coreutils)
base64 -w 0 HKA_Handball/hkahandball.keystore

# macOS
base64 HKA_Handball/hkahandball.keystore | tr -d '\n'

# Windows PowerShell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("HKA_Handball\hkahandball.keystore"))
```

Copy the full output string and paste it as the value of `ANDROID_KEYSTORE_BASE64`.

---

## 3. Bump the Version

Before every Play Store upload the **version code** (integer) must increase.
Edit `HKA_Handball/HKA_Handball.csproj`:

```xml
<!-- Human-readable version shown in the store -->
<ApplicationDisplayVersion>1.0.0</ApplicationDisplayVersion>

<!-- Integer that MUST increase with every Play Store upload -->
<ApplicationVersion>1</ApplicationVersion>
```

Bump `ApplicationVersion` by at least 1 for each release. Update
`ApplicationDisplayVersion` as you see fit (e.g., `1.1.0`, `2.0.0`).

---

## 4. Create a Release

Push a version tag to trigger the release workflow:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This triggers `.github/workflows/release.yml`, which:

1. Decodes the keystore from `ANDROID_KEYSTORE_BASE64`.
2. Runs `dotnet publish` in Release configuration with signing.
3. Packages native debug symbols for Google Play crash analysis.
4. Uploads the signed `.aab` and `native-debug-symbols.zip` as GitHub Actions artifacts.
5. Creates a GitHub Release with both files attached.
6. **Uploads the AAB to Google Play Console** as a draft on the internal testing
   track (if `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` is configured — see below).

Check the workflow run at **Actions → Release Android AAB** to verify it
succeeds, then download the AAB from the GitHub Release page.

---

## 5. Build Locally (optional)

If you prefer to build the signed AAB on your machine:

```bash
dotnet publish HKA_Handball/HKA_Handball.csproj \
  -f net10.0-android \
  -c Release \
  -p:AndroidSigningStorePass=YOUR_PASSWORD \
  -p:AndroidSigningKeyPass=YOUR_PASSWORD
```

The signed AAB will be in
`HKA_Handball/bin/Release/net10.0-android/publish/`.

---

## 6. Set Up Google Play Console

1. Go to [Google Play Console](https://play.google.com/console/) and sign in
   (a one-time $25 registration fee is required for a developer account).
2. **Create app** → fill in the app name, default language, app type (Game),
   and confirm the declarations.
3. Complete the **Dashboard checklist**. The main sections are:

| Section                 | What to do                                                                           |
| ----------------------- | ------------------------------------------------------------------------------------ |
| **Store listing**       | Use the text from [`STORE_LISTING.md`](STORE_LISTING.md). Upload screenshots.       |
| **Content rating**      | Fill the IARC questionnaire – the game has no violence, gambling, or user content.    |
| **Pricing & distribution** | Select **Free**. Choose target countries.                                          |
| **Privacy policy**      | Enter the URL: `https://github.com/KaptenJon/HKA_Handball/blob/master/PRIVACY_POLICY.md` |
| **App content**         | Declare ads (none), app access (no restrictions), data safety (no data collected).   |
| **Target audience**     | Select all ages (the game is suitable for everyone).                                 |

4. Under **App signing**, Google Play manages the distribution key. You sign the
   upload with *your* keystore; Google re-signs it for distribution. Just accept
   the default Google Play App Signing when prompted.

---

## 7. Automated Google Play Upload (optional)

The release workflow can automatically upload the AAB to Google Play Console when
you push a version tag. This removes the need to manually download and upload the
AAB each time. You still review and roll out the release in Google Play Console.

### Create a Google Play service account

1. In [Google Cloud Console](https://console.cloud.google.com/), open (or create)
   a project linked to your Google Play developer account.
2. Go to **IAM & Admin → Service Accounts → Create Service Account**.
3. Give it a name (e.g., `github-play-deploy`) and click **Done**.
4. On the service account row, click **⋮ → Manage keys → Add Key → Create new
   key → JSON**. Download the JSON file.
5. In [Google Play Console](https://play.google.com/console/) go to
   **Settings → API access** and link the Cloud project you just used.
6. Grant the service account **Release manager** (or at minimum **Release to
   production / Manage releases**) permission for your app.

### Add the secret to GitHub

Add the following **repository secret** (Settings → Secrets and variables →
Actions → New repository secret):

| Secret name                        | Value                                      |
| ---------------------------------- | ------------------------------------------ |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Full contents of the downloaded JSON file   |

When this secret is present the release workflow will upload the signed AAB and
native debug symbols to the **internal testing** track as a **draft**. You can
then open Google Play Console, add release notes, and promote the release to
production (or any other track) when ready.

> **Tip:** Start with the internal testing track to verify everything works
> before promoting to production. You can change the default track and status in
> `.github/workflows/release.yml` (look for the `track:` and `status:` inputs).

---

## 8. Upload the AAB manually (if not using automated upload)

1. In Google Play Console go to **Release → Production** (or start with
   **Internal testing** / **Closed testing** to test first).
2. Click **Create new release**.
3. Upload the `.aab` file from the GitHub Release (or your local build).
4. Under **Debug symbols**, upload the `native-debug-symbols.zip` from the
   GitHub Release. This resolves the "native code without debug symbols" warning
   and enables Google Play to symbolicate native crash reports.
5. Add release notes.
6. Click **Review release → Start rollout**.

> **ℹ️ Deobfuscation file:** Google Play may show a warning about a missing
> deobfuscation file (mapping.txt). This is an open-source project, so code
> obfuscation (R8/ProGuard) is unnecessary. You can safely ignore this warning.

Google will review the app (this can take a few hours to a few days for the
first submission).

---

## Quick-Reference Checklist

- [ ] Generate keystore (`generate-keystore.sh` or `generate-keystore.ps1`)
- [ ] Back up keystore and passwords securely
- [ ] Add three GitHub secrets (`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_PASSWORD`)
- [ ] Register a [Google Play Developer account](https://play.google.com/console/) ($25)
- [ ] Create the app in Google Play Console
- [ ] Fill in store listing, content rating, privacy policy, and app content
- [ ] Prepare store screenshots (phone and/or tablet, landscape)
- [ ] *(Optional)* Create a Google Play service account and add `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` secret for automated uploads
- [ ] Bump `ApplicationVersion` in `.csproj`
- [ ] Tag and push (`git tag v1.0.0 && git push origin v1.0.0`)
- [ ] Verify the release workflow succeeds
- [ ] If using automated upload: review the draft release in Google Play Console and promote it
- [ ] If uploading manually: download AAB from GitHub Release and upload it to Google Play Console
- [ ] Upload `native-debug-symbols.zip` under **Debug symbols** (automated upload includes this; manual upload requires this step)
- [ ] Submit for review
