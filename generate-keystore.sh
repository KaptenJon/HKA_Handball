#!/usr/bin/env bash
# Generate a release signing keystore for Google Play.
# Run this ONCE, then keep the .keystore file safe (back it up!).
# Never commit the keystore or passwords to git.

set -euo pipefail

KEYSTORE_PATH="HKA_Handball/hkahandball.keystore"

if [ -f "$KEYSTORE_PATH" ]; then
  echo "Keystore already exists at $KEYSTORE_PATH"
  echo "Delete it first if you really want to regenerate."
  exit 1
fi

echo "Generating signing keystore at $KEYSTORE_PATH ..."
echo "You will be prompted for a password. Use a strong one and save it securely."
echo ""

keytool -genkeypair \
  -v \
  -keystore "$KEYSTORE_PATH" \
  -alias hkahandball \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000 \
  -dname "CN=HKA Handball, O=KaptenJon, L=Sweden, C=SE"

echo ""
echo "Keystore created: $KEYSTORE_PATH"
echo ""
echo "Next steps:"
echo "  1. Back up the keystore and password somewhere safe."
echo "  2. Base64-encode for GitHub Actions:"
echo "       Linux:  base64 -w 0 $KEYSTORE_PATH"
echo "       macOS:  base64 -i $KEYSTORE_PATH"
echo "  3. Add these GitHub secrets:"
echo "       ANDROID_KEYSTORE_BASE64      (the base64 output)"
echo "       ANDROID_KEYSTORE_PASSWORD    (your store password)"
echo "       ANDROID_KEY_PASSWORD         (your key password)"
