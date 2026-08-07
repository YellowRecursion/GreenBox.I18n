package com.greenbox.i18n.rider;

/** Java equivalent of the self-identifying entry ID validation in GreenBox.I18n.Core. */
final class I18nEntryIdFormat {
    private static final int MAGIC = 0x6B1;
    private static final int CURRENT_VERSION = 0;
    private static final int VERSION_BIT_COUNT = 3;
    private static final int PAYLOAD_BIT_COUNT = 40;
    private static final int CHECKSUM_BIT_COUNT = 8;

    private I18nEntryIdFormat() {
    }

    static boolean isValid(String value) {
        final long parsedValue;
        try {
            parsedValue = Long.parseLong(value);
        } catch (NumberFormatException exception) {
            return false;
        }

        if (parsedValue <= 0) {
            return false;
        }

        long body = parsedValue >>> CHECKSUM_BIT_COUNT;
        int header = (int)(body >>> PAYLOAD_BIT_COUNT);
        int magic = header >>> VERSION_BIT_COUNT;
        int version = header & ((1 << VERSION_BIT_COUNT) - 1);
        int checksum = (int)(parsedValue & 0xFF);

        return magic == MAGIC &&
            version == CURRENT_VERSION &&
            checksum == computeChecksum(body);
    }

    private static int computeChecksum(long body) {
        int checksum = 0;
        for (int byteIndex = 6; byteIndex >= 0; byteIndex--) {
            checksum ^= (int)((body >>> (byteIndex * 8)) & 0xFF);
            for (int bitIndex = 0; bitIndex < 8; bitIndex++) {
                checksum = (checksum & 0x80) != 0
                    ? ((checksum << 1) ^ 0x07) & 0xFF
                    : (checksum << 1) & 0xFF;
            }
        }
        return checksum;
    }
}
