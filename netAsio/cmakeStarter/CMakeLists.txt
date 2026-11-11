cmake_minimum_required(VERSION 3.20)
project(ChatServer CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_compile_definitions(ASIO_STANDALONE)
include_directories(${ASIO_DIR}/include)            # cmake -DASIO_DIR=<Asio 폴더>
find_package(Threads REQUIRED)

add_executable(ChatServer ChatServer.cpp)
target_link_libraries(ChatServer Threads::Threads)

if(WIN32)
  add_compile_definitions(_WIN32_WINNT=0x0A00)      # Windows 10 이상
  target_link_libraries(ChatServer ws2_32 mswsock)
endif()
