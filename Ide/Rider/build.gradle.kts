plugins {
    java
    id("org.jetbrains.intellij.platform") version "2.10.5"
}

group = "com.greenbox.i18n"
version = file("../../eng/version.txt").readText().trim()

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

    publishing {
        token = providers.environmentVariable("PUBLISH_TOKEN")
    }
}

tasks {
    jar {
        from("../../LICENSE") {
            into("META-INF")
        }
        from("../../NOTICE") {
            into("META-INF")
        }
        from("../../THIRD-PARTY-NOTICES.md") {
            into("META-INF")
        }
    }

    runIde {
        maxHeapSize = "1500m"
    }
}
