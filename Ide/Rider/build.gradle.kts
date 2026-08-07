plugins {
    java
    id("org.jetbrains.intellij.platform") version "2.10.5"
}

group = "com.greenbox.i18n"
version = "0.3.0-dev"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

val riderHome = providers.gradleProperty("riderHome")
    .orElse(providers.environmentVariable("RIDER_HOME"))

dependencies {
    implementation("com.google.code.gson:gson:2.11.0")

    intellijPlatform {
        local(riderHome)
    }
}

java {
    toolchain {
        languageVersion = JavaLanguageVersion.of(21)
    }
}

intellijPlatform {
    buildSearchableOptions = false

    pluginConfiguration {
        ideaVersion {
            sinceBuild = "252"
            untilBuild = "252.*"
        }
    }
}

tasks {
    runIde {
        maxHeapSize = "1500m"
    }
}
