#include <stdio.h>
#include "vr_refresh_rate.h"

int main(void) {
    const float rates[] = {120, 72, 90, 80};
    const float low[] = {72, 80};
    if (CelesteXR_ChooseRefreshRate(72, rates, 4) != 90 ||
        CelesteXR_ChooseRefreshRate(90, rates, 4) != 0 ||
        CelesteXR_ChooseRefreshRate(120, rates, 4) != 0 ||
        CelesteXR_ChooseRefreshRate(72, low, 2) != 0 ||
        CelesteXR_ChooseRefreshRate(72, rates, 1) != 120) {
        fprintf(stderr, "VR refresh-rate selection failed.\n");
        return 1;
    }
    puts("VR refresh-rate selection checks passed.");
    return 0;
}
