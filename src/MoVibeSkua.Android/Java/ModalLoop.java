package com.arglax.movibeskua;
import android.os.Handler;
import android.os.Looper;
public final class ModalLoop {
    private static final class Quit extends RuntimeException {
        Quit() {
            super(null, null, false, false);
        }
    }
    private ModalLoop() {
    }
    public static void run() {
        try {
            Looper.loop();
        } catch (Quit ignored) {
        }
    }
    public static void quit() {
        new Handler(Looper.getMainLooper()).postAtFrontOfQueue(new Runnable() {
            @Override
            public void run() {
                throw new Quit();
            }
        });
    }
}
