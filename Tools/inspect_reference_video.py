from pathlib import Path
import bpy

root=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
editor=scene.sequence_editor_create()
strip=editor.strips.new_movie('Reference',str(root/'sample/need_to_build_like_this.mp4'),channel=1,frame_start=1)
print('VIDEO_FRAMES',strip.frame_duration,'FPS',strip.fps)
scene.render.resolution_x=960
scene.render.resolution_y=540
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.use_sequencer=True
scene.view_settings.view_transform='Standard'
output=root/'Temp/reference-frames'
output.mkdir(parents=True,exist_ok=True)
for index,fraction in enumerate([0.05,0.2,0.4,0.6,0.8,0.95]):
    scene.frame_set(max(1,int(strip.frame_duration*fraction)))
    scene.render.filepath=str(output/f'{index}.png')
    bpy.ops.render.render(write_still=True)
