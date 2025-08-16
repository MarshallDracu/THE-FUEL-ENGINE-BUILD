#include "imgui.h"
#include "imgui_internal.h"
#include "imgui_freetype.h"

namespace cimgui
{
#include "dcimgui_manual.h"
}

CIMGUI_API void cimgui::ImFontConfig_Construct(cimgui::ImFontConfig* self)
{
    // Placement-new doesn't need the pointer cast; it accepts void*
    IM_PLACEMENT_NEW(self) ::ImFontConfig();
}

CIMGUI_API void cimgui::ImGuiFreeType_AddTintIcon(ImWchar codepoint, ImWchar icon, ImU32 color)
{
    ImGuiFreeType::AddTintIcon(codepoint, icon, color);
}

CIMGUI_API void cimgui::ImGui_SeparatorEx(cimgui::ImGuiSeparatorFlags flags, float thickness)
{
    // Cast via the underlying integer, then to the native ImGui enum
    ::ImGuiSeparatorFlags native =
        static_cast<::ImGuiSeparatorFlags>(static_cast<int>(flags));
    ImGui::SeparatorEx(native, thickness);
}
