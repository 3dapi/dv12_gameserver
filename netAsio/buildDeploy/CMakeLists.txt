cmake_minimum_required(VERSION 3.20)
project(ChatServer VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 빌드 종류를 지정하지 않으면 최적화 + 심볼(RelWithDebInfo)로 만든다.
# Visual Studio처럼 구성이 여러 개인 생성기는 --config Release 등으로 고른다.
if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  set(CMAKE_BUILD_TYPE RelWithDebInfo)
endif()

option(ENABLE_SANITIZERS "ASan/UBSan을 켠다 (g++, clang)" OFF)
if(ENABLE_SANITIZERS AND NOT MSVC)
  add_compile_options(-fsanitize=address,undefined -fno-omit-frame-pointer)
  add_link_options(-fsanitize=address,undefined)
endif()

# 버전: 어느 소스로 만든 서버인지 알 수 있도록 git 커밋을 붙인다 (CMake를 설정하는 시점의 값이다).
execute_process(COMMAND git rev-parse --short HEAD
                WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
                OUTPUT_VARIABLE GIT_SHA OUTPUT_STRIP_TRAILING_WHITESPACE ERROR_QUIET)
if(NOT GIT_SHA)
  set(GIT_SHA "nogit")
endif()
set(SERVER_VERSION "${PROJECT_VERSION}-${GIT_SHA}")
message(STATUS "ChatServer version ${SERVER_VERSION}")

add_compile_definitions(ASIO_STANDALONE)
include_directories(${ASIO_DIR}/include)            # cmake -DASIO_DIR=<Asio 폴더>
find_package(Threads REQUIRED)
if(WIN32)
  add_compile_definitions(_WIN32_WINNT=0x0A00)      # Windows 10 이상
endif()

function(add_asio_program name source)              # 서버와 시험 프로그램이 같은 설정을 쓴다
  add_executable(${name} ${source})
  target_link_libraries(${name} Threads::Threads)
  if(WIN32)
    target_link_libraries(${name} ws2_32 mswsock)
  endif()
endfunction()

add_asio_program(ChatServer ChatServer.cpp)
target_compile_definitions(ChatServer PRIVATE SERVER_VERSION="${SERVER_VERSION}")
add_asio_program(IntegrationTest IntegrationTest.cpp)   # 서버를 먼저 실행한 뒤 직접 실행한다

enable_testing()                                    # 단위 시험: ctest로 실행한다
add_executable(test_protocol test_protocol.cpp)
add_test(NAME protocol COMMAND test_protocol)

# 배포 묶음에 들어갈 것: 실행 파일과 설정 견본 (심볼 파일은 따로 보관한다)
install(TARGETS ChatServer RUNTIME DESTINATION .)
install(FILES server.example.ini DESTINATION .)
