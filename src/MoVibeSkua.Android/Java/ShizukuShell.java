package com.arglax.movibeskua;
import android.content.pm.PackageManager;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.lang.reflect.Method;
import rikka.shizuku.Shizuku;
import rikka.shizuku.ShizukuRemoteProcess;
public final class ShizukuShell {
    private ShizukuShell() {
    }
    public static boolean available() {
        try {
            return Shizuku.pingBinder() && !Shizuku.isPreV11();
        } catch (Throwable t) {
            return false;
        }
    }
    public static boolean granted() {
        try {
            return available() && Shizuku.checkSelfPermission() == PackageManager.PERMISSION_GRANTED;
        } catch (Throwable t) {
            return false;
        }
    }
    public static boolean denied() {
        try {
            return available() && Shizuku.shouldShowRequestPermissionRationale();
        } catch (Throwable t) {
            return false;
        }
    }
    public static void request(int code) {
        try {
            if (available())
                Shizuku.requestPermission(code);
        } catch (Throwable ignored) {
        }
    }
    public static String exec(String command) {
        if (!granted())
            return null;
        try {
            Method m = Shizuku.class.getDeclaredMethod("newProcess", String[].class, String[].class, String.class);
            m.setAccessible(true);
            ShizukuRemoteProcess p = (ShizukuRemoteProcess) m.invoke(null, new String[] { "sh", "-c", command }, null, null);
            InputStream in = p.getInputStream();
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[16384];
            int n;
            while ((n = in.read(buf)) > 0)
                out.write(buf, 0, n);
            p.waitFor();
            return out.toString("UTF-8");
        } catch (Throwable t) {
            return null;
        }
    }
}
