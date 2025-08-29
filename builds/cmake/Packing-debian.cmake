message(STATUS "Package configuration (only DEB for now)")
set(BASE_PACKAGE_NAME ${PROJECT_NAME})
option(DEB_NO_STRIP "Do not strip binaries when generating the .deb package" OFF)
# these are cache variables, so they could be overwritten with -D,
if (${CMAKE_BUILD_TYPE} STREQUAL "Debug")
    message(STATUS "Debug build, binaries with debug symbols will be used for .deb package")
    set(CPACK_PACKAGE_NAME ${BASE_PACKAGE_NAME}-debug-dev CACHE STRING "The resulting package name")
    set(CPACK_DEBIAN_PACKAGE_CONFLICTS "${BASE_PACKAGE_NAME}-dev, ${BASE_PACKAGE_NAME}-no-strip-dev")
elseif (${DEB_NO_STRIP})
    message(STATUS "DEB_NO_STRIP activated, binaries won't be stripped of symbols when generating the .deb package")
    set(CPACK_PACKAGE_NAME ${BASE_PACKAGE_NAME}-no-strip-dev CACHE STRING "The resulting package name")
    set(CPACK_DEBIAN_PACKAGE_CONFLICTS "${BASE_PACKAGE_NAME}-dev, ${BASE_PACKAGE_NAME}-debug-dev")
else()
    message(STATUS "Binaries will be stripped of debug symbols.")
    message(STATUS "  Set -DCMAKE_BUILD_TYPE=Debug to create a package with debug symbols.")
    message(STATUS "  Set -DDEB_NO_STRIP=ON to have release binaries not stripped when generating the .deb package.")
    set(CPACK_PACKAGE_NAME ${BASE_PACKAGE_NAME}-dev CACHE STRING "The resulting package name")
    set(CPACK_DEBIAN_PACKAGE_CONFLICTS "${BASE_PACKAGE_NAME}-debug-dev, ${BASE_PACKAGE_NAME}-no-strip-dev")
endif()

# which is useful in case of packing only selected components instead of the whole thing
set(CPACK_PACKAGE_DESCRIPTION_SUMMARY "Model-based framework for broker-free distributed software environments - development package" CACHE STRING "Package description summary")
set(CPACK_PACKAGE_VENDOR "Ingescape")

set(CPACK_VERBATIM_VARIABLES YES)

set(CPACK_PACKAGE_INSTALL_DIRECTORY ${CPACK_PACKAGE_NAME})
SET(CPACK_OUTPUT_FILE_PREFIX "${CMAKE_SOURCE_DIR}/_packages")

# https://unix.stackexchange.com/a/11552/254512
set(CPACK_PACKAGING_INSTALL_PREFIX "/usr")#/${CMAKE_PROJECT_VERSION}")

set(CPACK_PACKAGE_VERSION_MAJOR ${PROJECT_VERSION_MAJOR})
set(CPACK_PACKAGE_VERSION_MINOR ${PROJECT_VERSION_MINOR})
set(CPACK_PACKAGE_VERSION_PATCH ${PROJECT_VERSION_PATCH})

set(CPACK_PACKAGE_CONTACT "github@ingescape.com")
set(CPACK_DEBIAN_PACKAGE_MAINTAINER "Ingenuity I/O <${CPACK_PACKAGE_CONTACT}>")

set(CPACK_RESOURCE_FILE_README "${CMAKE_CURRENT_SOURCE_DIR}/README.md")
set(CPACK_RESOURCE_FILE_LICENSE "${CMAKE_CURRENT_SOURCE_DIR}/LICENSE")
install(FILES ${CMAKE_CURRENT_SOURCE_DIR}/LICENSE
        DESTINATION share/doc/${CPACK_PACKAGE_NAME}
        RENAME copyright)


# Strip binaries of debuging names and symbols
if ((${CMAKE_BUILD_TYPE} STREQUAL "Debug") OR ${DEB_NO_STRIP})
    message(STATUS "Binaries will NOT be stripped when generating .deb package")
    set(CPACK_STRIP_FILES NO)
else()
    message(STATUS "Binaries will be stripped when generating .deb package")
    set(CPACK_STRIP_FILES YES)
endif()

# Set dependencies line in package control file
set(CPACK_DEBIAN_PACKAGE_SHLIBDEPS YES)

# License
set (CPACK_RESOURCE_FILE_LICENSE "${CMAKE_CURRENT_SOURCE_DIR}/LICENSE")


# package name for deb
# if set, then instead of some-application-0.9.2-Linux.deb
# you'll get some-application_0.9.2_amd64.deb (note the underscores too)
set(CPACK_DEBIAN_FILE_NAME DEB-DEFAULT)
# if you want every group to have its own package,
# although the same happens if this is not sent (so it defaults to ONE_PER_GROUP)
# and CPACK_DEB_COMPONENT_INSTALL is set to YES
set(CPACK_COMPONENTS_GROUPING ALL_COMPONENTS_IN_ONE)#ONE_PER_GROUP)
# without this you won't be able to pack only specified component
set(CPACK_DEB_COMPONENT_INSTALL YES)

message(STATUS "Components to pack: ${CPACK_COMPONENTS_ALL}")

include(CPack)
