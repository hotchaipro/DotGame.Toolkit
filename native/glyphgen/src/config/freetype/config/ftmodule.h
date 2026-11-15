// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

/*
 * FreeType module list for glyphgen. Shadows freetype/config/ftmodule.h
 * (this directory is placed first on the include path) so only the
 * modules needed for TrueType / OpenType (CFF) outlines and hinted
 * grayscale rendering are compiled in.
 */

FT_USE_MODULE( FT_Module_Class, autofit_module_class )
FT_USE_MODULE( FT_Driver_ClassRec, tt_driver_class )
FT_USE_MODULE( FT_Driver_ClassRec, cff_driver_class )
FT_USE_MODULE( FT_Module_Class, psaux_module_class )
FT_USE_MODULE( FT_Module_Class, psnames_module_class )
FT_USE_MODULE( FT_Module_Class, pshinter_module_class )
FT_USE_MODULE( FT_Module_Class, sfnt_module_class )
FT_USE_MODULE( FT_Renderer_Class, ft_smooth_renderer_class )
FT_USE_MODULE( FT_Renderer_Class, ft_raster1_renderer_class )

/* EOF */
