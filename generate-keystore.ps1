# Generate a release signing keystore for Google Play
# Run this ONCE, then keep the .keystore file safe (back it up!)
# Never commit the keystore or passwords to git.

keytool -genkeypair `
  -v `
  -keystore HKA_Handball/hkahandball.keystore `
  -alias hkahandball `
  -keyalg RSA `
  -keysize 2048 `
  -validity 10000 `
  -storepass REPLACE_WITH_SECURE_PASSWORD `
  -keypass REPLACE_WITH_SECURE_PASSWORD `
  -dname "CN=HKA Handball, O=KaptenJon, L=Sweden, C=SE"

Write-Host ""
Write-Host "Keystore created: HKA_Handball/hkahandball.keystore"
Write-Host ""
Write-Host "IMPORTANT: Update the signing passwords in your build command:"
Write-Host '  dotnet publish -f net10.0-android -c Release -p:AndroidSigningStorePass=YOUR_PASSWORD -p:AndroidSigningKeyPass=YOUR_PASSWORD'
