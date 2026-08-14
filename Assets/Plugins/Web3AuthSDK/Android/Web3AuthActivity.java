package com.web3auth.unity.android;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;

import com.unity3d.player.UnityPlayerActivity;

/**
 * Captures torusapp:// Custom Tab redirects. setIntent is required for singleTask + Unity deep links.
 */
public class Web3AuthActivity extends UnityPlayerActivity {
    private static final String TAG = "Web3AuthActivity";
    private static String pendingDeepLinkUrl;

    public static synchronized String consumePendingDeepLinkUrl() {
        String url = pendingDeepLinkUrl;
        pendingDeepLinkUrl = null;
        return url;
    }

    public static synchronized void setPendingDeepLinkUrl(String url) {
        pendingDeepLinkUrl = url;
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        captureDeepLink(getIntent());
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        captureDeepLink(intent);
    }

    private void captureDeepLink(Intent intent) {
        if (intent == null) {
            return;
        }
        Uri data = intent.getData();
        if (data == null) {
            return;
        }
        String url = data.toString();
        if (url == null || url.length() == 0) {
            return;
        }
        if (!url.startsWith("torusapp://")) {
            return;
        }
        Log.d(TAG, "Captured deep link: " + url);
        setPendingDeepLinkUrl(url);
        intent.setData(null);
    }
}
