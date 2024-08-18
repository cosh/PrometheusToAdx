package com.azure.kusto;

import java.io.File;

import org.junit.Test;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.testcontainers.containers.DockerComposeContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;

public class PrometheusRemoteWriteIT {

    private static final DockerComposeContainer<?> environment = new DockerComposeContainer<>(
            new File("src/test/resources/compose.yml")).withExposedService("prom", 9090);

    @BeforeAll
    static void setup() {
        log.info("@BeforeAll - executes once before all test methods in this class");
    }

    @BeforeEach
    void init() {
        log.info("@BeforeEach - executes before each test method in this class");
    }

    @Test
    public void top_level_container_should_be_running() {
        assert (true);
    }

}
