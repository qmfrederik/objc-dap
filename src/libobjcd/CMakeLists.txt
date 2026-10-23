cmake_minimum_required(VERSION 3.10)

project(objcd)

find_package(libobjc2)
find_package(gnustep-base)

add_library(objcd objcd.m)
