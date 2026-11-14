// MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
//
// Library-level functions, compiled into every native library of this repository.
#include <cstdlib>

#include "internal.hpp"

extern "C" {

int32_t noetix_bumi_c_api_version(void) { return NOETIX_BUMI_C_API_VERSION; }

const char* noetix_bumi_sdk_version(void) { return SDK_VERSION; }

const char* noetix_bumi_last_error(void) { return bumi::last_error.c_str(); }

int32_t noetix_bumi_set_dds_config(const char* path) {
    if (!path || !*path) {
        return bumi::invalid("path");
    }
    const std::string uri = std::string("file://") + path;
    if (setenv("CYCLONEDDS_URI", uri.c_str(), 1) != 0) {
        bumi::set_error("setenv(CYCLONEDDS_URI) failed");
        return NOETIX_ERR_EXCEPTION;
    }
    return NOETIX_OK;
}

} // extern "C"
