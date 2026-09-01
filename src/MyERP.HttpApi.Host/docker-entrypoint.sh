#!/bin/sh
# Generates a self-signed OpenIddict signing/encryption certificate on first boot
# if one doesn't already exist at AuthServer__CertificatePath, so a fresh
# self-hosted deployment doesn't need a manually-provisioned .pfx file.
# The cert is expected to live on a persisted volume so tokens stay valid
# across container restarts/recreations.
set -e

# Development uses ABP's built-in ephemeral dev certificate — no .pfx needed.
if [ "$ASPNETCORE_ENVIRONMENT" != "Development" ]; then
    CERT_PATH="${AuthServer__CertificatePath:-openiddict.pfx}"
    CERT_PASSPHRASE="${AuthServer__CertificatePassPhrase:?AuthServer__CertificatePassPhrase must be set outside Development}"

    if [ ! -f "$CERT_PATH" ]; then
        echo "No signing certificate found at $CERT_PATH — generating a self-signed one..."
        mkdir -p "$(dirname "$CERT_PATH")"
        TMP_KEY=$(mktemp)
        TMP_CRT=$(mktemp)
        openssl req -x509 -newkey rsa:2048 -keyout "$TMP_KEY" -out "$TMP_CRT" \
            -days 3650 -nodes -subj "/CN=MyERP OpenIddict"
        openssl pkcs12 -export -out "$CERT_PATH" -inkey "$TMP_KEY" -in "$TMP_CRT" \
            -passout "pass:$CERT_PASSPHRASE"
        rm -f "$TMP_KEY" "$TMP_CRT"
        echo "Certificate generated at $CERT_PATH"
    fi
fi

exec dotnet MyERP.HttpApi.Host.dll
