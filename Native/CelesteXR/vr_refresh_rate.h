#ifndef CELESTE_VR_REFRESH_RATE_H
#define CELESTE_VR_REFRESH_RATE_H

#include <stdint.h>

/* Zero means keep the runtime's current rate. Never downgrade 90/120 Hz. */
static float CelesteXR_ChooseRefreshRate(float current, const float *rates, uint32_t count) {
    if (current >= 89.5f) return 0;
    float chosen = 0;
    for (uint32_t i = 0; i < count; i++)
        if (rates[i] >= 89.5f && (!chosen || rates[i] < chosen)) chosen = rates[i];
    return chosen;
}
#endif
