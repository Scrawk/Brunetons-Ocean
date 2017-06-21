# Brunetons-Ocean

This is a ocean Ive ported to Unity and is based Eric Brunetons [ocean renderer](http://evasion.inrialpes.fr/~Eric.Bruneton/).

The focus of the project is the BRDF lighting model which looks very nice and is coupled with his previous work on atmospheric scattering.

You can find some settings on the ocean game object in the scene. You can change the wind speed, wave decay factor, wave amplitude, and the Fourier transform size. These settings are only used on start up however and will have no effect during run time. If you want to change them during run time you will need to regenerate the wave spectrum. Its only done on start up in this project. If your looking for what setting will make the waves bigger its the wind speed.

See home page for more information.

![Brunetons Ocean](https://static.wixstatic.com/media/1e04d5_31b8c4569ee049a8927c7a2260e1ef30~mv2.jpg/v1/fill/w_550,h_550,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_31b8c4569ee049a8927c7a2260e1ef30~mv2.jpg)

In Erics project he did use a projected grid for the ocean mesh but the implementation was not really robust enough to be practical. I have used a different implementation based on this [paper](http://fileadmin.cs.lth.se/graphics/theses/projects/projgrid/). I have left it as simple as possible so it will work but you may see the grid pull away from the screen if the waves are to large.

 
Below is a image of the projected grid. You can see the mesh is projected from the camera and its shape matches the camera frustum.

![Projected Grid](https://static.wixstatic.com/media/1e04d5_df61c83d283c4f599208435360c74619~mv2.jpg/v1/fill/w_550,h_248,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_df61c83d283c4f599208435360c74619~mv2.jpg)
