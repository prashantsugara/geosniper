package com.geosniper.map;

import android.app.Activity;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.Typeface;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.TextView;

import com.unity3d.player.UnityPlayer;

import org.maplibre.android.MapLibre;
import org.maplibre.android.annotations.IconFactory;
import org.maplibre.android.annotations.MarkerOptions;
import org.maplibre.android.geometry.LatLng;
import org.maplibre.android.camera.CameraPosition;
import org.maplibre.android.maps.MapView;
import org.maplibre.android.maps.MapLibreMap;
import org.maplibre.android.maps.OnMapReadyCallback;
import org.maplibre.android.maps.Style;

public final class MapLibreBridge {
    private static MapView mapView;
    private static FrameLayout overlay;
    private static String callbackObject = "Geo Sniper";

    private MapLibreBridge() { }

    public static boolean open(final double latitude, final double longitude, final String callback, final float heading, final String worldStyle) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) return false;
        callbackObject = callback == null ? "Geo Sniper" : callback;
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    closeInternal(false);
                    MapLibre.getInstance(activity.getApplicationContext());
                    overlay = new FrameLayout(activity);
                    overlay.setBackgroundColor(Color.rgb(10, 19, 25));
                    mapView = new MapView(activity);
                    mapView.onCreate(null);
                    mapView.onStart();
                    mapView.onResume();
                    overlay.addView(mapView, new FrameLayout.LayoutParams(-1, -1));

                    Button close = new Button(activity);
                    close.setText("CLOSE MAP");
                    close.setTextColor(Color.WHITE);
                    close.setTextSize(12);
                    close.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
                    close.setSingleLine(true);
                    close.setPadding(0, 0, 0, 0);
                    close.setBackgroundColor(Color.rgb(35, 48, 56));
                    close.setOnClickListener(new View.OnClickListener() {
                        @Override public void onClick(View view) {
                            closeInternal(true);
                            UnityPlayer.UnitySendMessage(callbackObject, "OnNativeMapClosed", "");
                        }
                    });
                    float density = activity.getResources().getDisplayMetrics().density;
                    FrameLayout.LayoutParams closeParams = new FrameLayout.LayoutParams((int)(132*density), (int)(48*density), Gravity.TOP | Gravity.RIGHT);
                    closeParams.setMargins(0, (int)(12*density), (int)(12*density), 0);
                    overlay.addView(close, closeParams);

                    TextView attribution = new TextView(activity);
                    attribution.setText(worldStyle == null || worldStyle.isEmpty()
                        ? "© OpenStreetMap contributors | MapLibre"
                        : "Overture Maps / © OpenStreetMap contributors | Downloaded area");
                    attribution.setTextColor(Color.WHITE);
                    attribution.setTextSize(11);
                    attribution.setBackgroundColor(Color.argb(175, 10, 19, 25));
                    attribution.setPadding(12, 6, 12, 6);
                    FrameLayout.LayoutParams attributionParams = new FrameLayout.LayoutParams(-2, -2, Gravity.BOTTOM | Gravity.LEFT);
                    attributionParams.setMargins(18, 0, 0, 18);
                    overlay.addView(attribution, attributionParams);

                    ((FrameLayout)activity.findViewById(android.R.id.content)).addView(overlay,
                        new FrameLayout.LayoutParams(-1, -1));
                    mapView.getMapAsync(new OnMapReadyCallback() {
                        @Override public void onMapReady(MapLibreMap map) {
                            map.getUiSettings().setRotateGesturesEnabled(false);
                            map.getUiSettings().setTiltGesturesEnabled(false);
                            map.setStyle(new Style.Builder().fromJson(worldStyle == null || worldStyle.isEmpty() ? styleJson() : worldStyle), new Style.OnStyleLoaded() {
                                @Override public void onStyleLoaded(Style style) {
                                    if (mapView == null) return;
                                    map.addMarker(new MarkerOptions()
                                        .position(new LatLng(latitude, longitude))
                                        .title("YOU ARE HERE")
                                        .icon(IconFactory.getInstance(activity).fromBitmap(directionBitmap(heading))));
                                }
                            });
                            // Match the 640 m gameplay sector instead of showing several city blocks.
                            map.setCameraPosition(new CameraPosition.Builder()
                                .target(new LatLng(latitude, longitude)).zoom(17.2).build());
                        }
                    });
                } catch (Throwable error) {
                    closeInternal(false);
                    UnityPlayer.UnitySendMessage(callbackObject, "OnNativeMapFailed", error.getClass().getSimpleName());
                }
            }
        });
        return true;
    }

    private static Bitmap directionBitmap(float heading) {
        // The map stays north-up, so bake the Unity yaw into a simple player arrow.
        Bitmap bitmap = Bitmap.createBitmap(72, 72, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(bitmap);
        canvas.save();
        canvas.rotate(heading, 36, 36);
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setColor(Color.rgb(0, 235, 225));
        paint.setStyle(Paint.Style.FILL);
        Path arrow = new Path();
        arrow.moveTo(36, 6);
        arrow.lineTo(54, 56);
        arrow.lineTo(36, 47);
        arrow.lineTo(18, 56);
        arrow.close();
        canvas.drawPath(arrow, paint);
        paint.setColor(Color.rgb(8, 35, 42));
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(3);
        canvas.drawPath(arrow, paint);
        canvas.restore();
        return bitmap;
    }

    public static void close() {
        Activity activity = UnityPlayer.currentActivity;
        if (activity != null) activity.runOnUiThread(new Runnable() {
            @Override public void run() { closeInternal(false); }
        });
    }

    private static void closeInternal(boolean notify) {
        if (mapView != null) {
            mapView.onPause();
            mapView.onStop();
            mapView.onDestroy();
            mapView = null;
        }
        if (overlay != null) {
            if(overlay.getParent() instanceof FrameLayout)
                ((FrameLayout)overlay.getParent()).removeView(overlay);
            overlay = null;
        }
    }

    private static String styleJson() {
        return "{\"version\":8,\"name\":\"Geo Sniper OSM\",\"sources\":{"+
            "\"osm\":{\"type\":\"raster\",\"tiles\":[\"https://tile.openstreetmap.org/{z}/{x}/{y}.png\"],\"tileSize\":256,\"attribution\":\"© OpenStreetMap contributors\"}},"+
            "\"layers\":[{\"id\":\"osm\",\"type\":\"raster\",\"source\":\"osm\"}]}";
    }
}
